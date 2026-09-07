namespace PlanetSurvival.Crafting.Domain
{
    public readonly struct CraftingResult
    {
        private CraftingResult(bool succeeded, CraftingFailure failure, string message)
        {
            Succeeded = succeeded;
            Failure = failure;
            Message = message;
        }

        public bool Succeeded { get; }
        public CraftingFailure Failure { get; }
        public string Message { get; }

        public static CraftingResult Success()
        {
            return new CraftingResult(true, CraftingFailure.None, string.Empty);
        }

        public static CraftingResult Fail(CraftingFailure failure, string message)
        {
            return new CraftingResult(false, failure, message);
        }
    }
}
