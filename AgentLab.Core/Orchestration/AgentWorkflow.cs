using AgentLab.Core.Agents;

namespace AgentLab.Core.Orchestration
{
    public class AgentWorkflow(FixerAgent fixer, ReviewerAgent reviewer, DeveloperAgent developer, PlannerAgent planner)
    {
        private readonly PlannerAgent _planner = planner;
        private readonly DeveloperAgent _developer = developer;
        private readonly ReviewerAgent _reviewer = reviewer;
        private readonly FixerAgent _fixer = fixer;

        public async Task<WorkflowResult> ExecuteAsync(
        CodingTask task,
        CancellationToken cancellationToken = default)
        {
            var plan = await _planner.ExecuteAsync(
                task,
                cancellationToken);

            var implementation = await _developer.ExecuteAsync(
                new DeveloperInput(task,plan),
                cancellationToken);

            ReviewResult? review = null;

            const int maxRetries = 2;

            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                review = await _reviewer.ExecuteAsync(
                    new ReviewInput(
                        task,
                        plan,
                        implementation),
                    cancellationToken);

                if (review.Approved)
                    break;

                if (attempt == maxRetries)
                    break;

                implementation = await _fixer.ExecuteAsync(
                    new FixInput(
                        task,
                        plan,
                        implementation,
                        review),
                    cancellationToken);
            }

            return new WorkflowResult(
                plan,
                implementation,
                review!);
        }
    }
}
