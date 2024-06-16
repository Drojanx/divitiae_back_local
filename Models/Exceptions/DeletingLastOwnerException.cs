namespace divitiae_api.Models.Exceptions
{
    public class DeletingLastOwnerException : Exception
    {
        public DeletingLastOwnerException()
            : base($"This user can't be removed because it's the last owner of the environment.")
        {

        }

    }
}
