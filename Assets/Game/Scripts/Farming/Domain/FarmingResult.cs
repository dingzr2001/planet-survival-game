namespace PlanetSurvival.Farming.Domain
{
    public readonly struct FarmingResult
    {
        private FarmingResult(bool succeeded, FarmingFailure failure, string message)
        {
            Succeeded = succeeded;
            Failure = failure;
            Message = message;
        }

        public bool Succeeded { get; }
        public FarmingFailure Failure { get; }
        public string Message { get; }

        public static FarmingResult Success()
        {
            return new FarmingResult(true, FarmingFailure.None, string.Empty);
        }

        public static FarmingResult Fail(FarmingFailure failure, string message)
        {
            return new FarmingResult(false, failure, message);
        }
    }
}
