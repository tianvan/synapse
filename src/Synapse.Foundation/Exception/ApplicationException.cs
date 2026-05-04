namespace Synapse.Foundation.Exception;

public class ApplicationException(string message) : System.Exception(message)
{
    public ApplicationException(string message, System.Exception inner)
        : this(message) { }
}
