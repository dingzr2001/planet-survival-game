namespace PlanetSurvival.Building.Domain
{
    public readonly struct BuildResult
    {
        private BuildResult(bool succeeded, BuildFailure failure, string message)
        {
            Succeeded = succeeded;
            Failure = failure;
            Message = message;
        }

        public bool Succeeded { get; }
        public BuildFailure Failure { get; }
        public string Message { get; }

        public static BuildResult Success()
        {
            return new BuildResult(true, BuildFailure.None, string.Empty);
        }

        public static BuildResult Fail(BuildFailure failure, string message)
        {
            return new BuildResult(false, failure, message);
        }
    }
}
