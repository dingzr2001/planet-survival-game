namespace PlanetSurvival.Inventory.Domain
{
    public readonly struct InventoryOperationResult
    {
        private InventoryOperationResult(bool succeeded, InventoryFailure failure, string message)
        {
            Succeeded = succeeded;
            Failure = failure;
            Message = message;
        }

        public bool Succeeded { get; }
        public InventoryFailure Failure { get; }
        public string Message { get; }

        public static InventoryOperationResult Success()
        {
            return new InventoryOperationResult(true, InventoryFailure.None, string.Empty);
        }

        public static InventoryOperationResult Fail(InventoryFailure failure, string message)
        {
            return new InventoryOperationResult(false, failure, message);
        }
    }
}
