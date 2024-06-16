namespace divitiae_api.Models.Exceptions
{
    public class DeletingOtherOwnerException : Exception
    {
        public DeletingOtherOwnerException()
            : base($"You can't remove another owner from the environment. They need to remove themselves.")
        {

        }


    }
}
