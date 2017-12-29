﻿namespace MvcForum.Web.Controllers
{
    using System;
    using System.Linq;
    using System.Web.Mvc;
    using Core.DomainModel.Entities;
    using Core.ExtensionMethods;
    using Core.Interfaces.Services;
    using Core.Interfaces.UnitOfWork;
    using ViewModels;

    [Authorize]
    public partial class PollController : BaseController
    {
        private readonly IPollAnswerService _pollAnswerService;
        private readonly IPollService _pollService;
        private readonly IPollVoteService _pollVoteService;

        public PollController(ILoggingService loggingService, IUnitOfWorkManager unitOfWorkManager,
            IMembershipService membershipService,
            ILocalizationService localizationService, IRoleService roleService, ISettingsService settingsService,
            IPollService pollService, IPollVoteService pollVoteService,
            IPollAnswerService pollAnswerService, ICacheService cacheService)
            : base(loggingService, unitOfWorkManager, membershipService, localizationService, roleService,
                settingsService, cacheService)
        {
            _pollService = pollService;
            _pollAnswerService = pollAnswerService;
            _pollVoteService = pollVoteService;
        }

        [HttpPost]
        public PartialViewResult UpdatePoll(UpdatePollViewModel updatePollViewModel)
        {
            using (var unitOfWork = UnitOfWorkManager.NewUnitOfWork())
            {
                try
                {
                    var loggedOnReadOnlyUser = User.GetMembershipUser(MembershipService);

                    // Fist need to check this user hasn't voted already and is trying to fudge the system
                    if (!_pollVoteService.HasUserVotedAlready(updatePollViewModel.AnswerId, loggedOnReadOnlyUser.Id))
                    {
                        var loggedOnUser = MembershipService.GetUser(loggedOnReadOnlyUser.Id);

                        // Get the answer
                        var pollAnswer = _pollAnswerService.Get(updatePollViewModel.AnswerId);

                        // create a new vote
                        var pollVote = new PollVote {PollAnswer = pollAnswer, User = loggedOnUser};

                        // Add it
                        _pollVoteService.Add(pollVote);

                        // Update the context so the changes are reflected in the viewmodel below
                        unitOfWork.SaveChanges();
                    }

                    // Create the view model and get ready return the poll partial view
                    var poll = _pollService.Get(updatePollViewModel.PollId);
                    var votes = poll.PollAnswers.SelectMany(x => x.PollVotes).ToList();
                    var alreadyVoted = votes.Count(x => x.User.Id == loggedOnReadOnlyUser.Id) > 0;
                    var viewModel = new PollViewModel
                    {
                        Poll = poll,
                        TotalVotesInPoll = votes.Count(),
                        UserHasAlreadyVoted = alreadyVoted
                    };

                    // Commit the transaction
                    unitOfWork.Commit();

                    return PartialView("_Poll", viewModel);
                }
                catch (Exception ex)
                {
                    unitOfWork.Rollback();
                    LoggingService.Error(ex);
                    throw new Exception(LocalizationService.GetResourceString("Errors.GenericMessage"));
                }
            }
        }
    }
}