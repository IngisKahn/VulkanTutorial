using System.Reflection;
using Silk.NET.Core.Native;
using Silk.NET.Windowing;
using ImageLayout = Silk.NET.Vulkan.ImageLayout;

namespace VulkanTutorial.DepthBuffering;
internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    static async Task Main()
    {
        VulkanWindow window = new(800, 600);
        VulkanRenderer renderer = new(window);
        window.Window.Run();
        renderer.WaitForIdle();
        renderer.Dispose();
    }
}
