using System.Runtime.Serialization;

namespace VulkanTutorial.TextureMapping;

[Serializable]
public class VulkanException : Exception
{
    //
    // For guidelines regarding the creation of new exception types, see
    //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
    // and
    //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
    //

    public VulkanException()
    {
    }

    public VulkanException(string message) : base(message)
    {
    }

    public VulkanException(string message, Exception inner) : base(message, inner)
    {
    }

    protected VulkanException(
        SerializationInfo info,
        StreamingContext context) : base(info, context)
    {
    }
}