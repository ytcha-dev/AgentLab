using AgentLab.Core.Agents;

namespace AgentLab.Core.Orchestration
{
    public class AgentWorkflow(FixerAgent fixer, ReviewerAgent reviewer, DeveloperAgent developer, PlannerAgent planner)
    {
        private readonly PlannerAgent _planner = planner;
        private readonly DeveloperAgent _developer = developer;
        private readonly ReviewerAgent _reviewer = reviewer;
        private readonly FixerAgent _fixer = fixer;

        public Task<WorkflowResult> ExecuteAsync(
            CodingTask task,
            CancellationToken cancellationToken = default) =>
            ExecuteAsync(
                task,
                RunContext.Create("software-development"),
                cancellationToken);

        public async Task<WorkflowResult> ExecuteAsync(
            CodingTask task,
            RunContext run,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(run);

            var steps = new List<AgentExecutionResult>();
            Plan? plan = null;
            Implementation? implementation = null;
            ReviewResult? review = null;
            var currentContext = run.ForStep("plan");

            try
            {
                var planStep = await _planner.ExecuteAsync(
                    task,
                    currentContext,
                    cancellationToken);
                steps.Add(planStep.Execution);

                if (!planStep.IsSuccess)
                {
                    return FromStoppedStep(
                        run,
                        plan,
                        implementation,
                        review,
                        steps,
                        planStep.Execution);
                }

                plan = planStep.Value!;
                currentContext = run.ForStep("develop");

                var developerStep = await _developer.ExecuteAsync(
                    new DeveloperInput(task, plan),
                    currentContext,
                    cancellationToken);
                steps.Add(developerStep.Execution);

                if (!developerStep.IsSuccess)
                {
                    return FromStoppedStep(
                        run,
                        plan,
                        implementation,
                        review,
                        steps,
                        developerStep.Execution);
                }

                implementation = developerStep.Value!;

                const int maxReviewAttempts = 3;

                for (var reviewAttempt = 1;
                     reviewAttempt <= maxReviewAttempts;
                     reviewAttempt++)
                {
                    currentContext = run.ForStep("review", reviewAttempt);

                    var reviewStep = await _reviewer.ExecuteAsync(
                        new ReviewInput(
                            task,
                            plan,
                            implementation),
                        currentContext,
                        cancellationToken);
                    steps.Add(reviewStep.Execution);

                    if (!reviewStep.IsSuccess)
                    {
                        return FromStoppedStep(
                            run,
                            plan,
                            implementation,
                            review,
                            steps,
                            reviewStep.Execution);
                    }

                    review = reviewStep.Value!;

                    if (review.Approved)
                    {
                        return new WorkflowResult(
                            run,
                            WorkflowStatus.Succeeded,
                            plan,
                            implementation,
                            review,
                            steps,
                            null);
                    }

                    if (reviewAttempt == maxReviewAttempts)
                    {
                        break;
                    }

                    currentContext = run.ForStep("fix", reviewAttempt);

                    var fixerStep = await _fixer.ExecuteAsync(
                        new FixInput(
                            task,
                            plan,
                            implementation,
                            review),
                        currentContext,
                        cancellationToken);
                    steps.Add(fixerStep.Execution);

                    if (!fixerStep.IsSuccess)
                    {
                        return FromStoppedStep(
                            run,
                            plan,
                            implementation,
                            review,
                            steps,
                            fixerStep.Execution);
                    }

                    implementation = fixerStep.Value!;
                }

                return new WorkflowResult(
                    run,
                    WorkflowStatus.NotApproved,
                    plan,
                    implementation,
                    review,
                    steps,
                    null);
            }
            catch (OperationCanceledException ex)
            {
                return FromUnexpectedFailure(
                    run,
                    plan,
                    implementation,
                    review,
                    steps,
                    currentContext,
                    WorkflowStatus.Cancelled,
                    AgentFailureType.Cancelled,
                    ex.Message);
            }
            catch (Exception ex)
            {
                return FromUnexpectedFailure(
                    run,
                    plan,
                    implementation,
                    review,
                    steps,
                    currentContext,
                    WorkflowStatus.Failed,
                    ex is AgentExecutionException agentException
                        ? agentException.FailureType
                        : AgentFailureType.ModelError,
                    ex.Message);
            }
        }

        private static WorkflowResult FromStoppedStep(
            RunContext run,
            Plan? plan,
            Implementation? implementation,
            ReviewResult? review,
            IReadOnlyList<AgentExecutionResult> steps,
            AgentExecutionResult stoppedStep) =>
            new(
                run,
                stoppedStep.Status == StepStatus.Cancelled
                    ? WorkflowStatus.Cancelled
                    : WorkflowStatus.Failed,
                plan,
                implementation,
                review,
                steps,
                stoppedStep.Failure);

        private static WorkflowResult FromUnexpectedFailure(
            RunContext run,
            Plan? plan,
            Implementation? implementation,
            ReviewResult? review,
            IReadOnlyList<AgentExecutionResult> steps,
            AgentExecutionContext context,
            WorkflowStatus status,
            AgentFailureType failureType,
            string message) =>
            new(
                run,
                status,
                plan,
                implementation,
                review,
                steps,
                new FailureInfo(
                    failureType,
                    context.StepId,
                    message,
                    false,
                    context.Attempt,
                    DateTimeOffset.UtcNow));
    }
}
