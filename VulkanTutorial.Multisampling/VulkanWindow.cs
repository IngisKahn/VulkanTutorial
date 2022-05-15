using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace VulkanTutorial.Multisampling;

public sealed class VulkanWindow
{

    private const bool EventBasedRendering = false;
    private readonly IWindow window;
    public IWindow Window => this.window;

    public VulkanWindow(int width, int height)
    {
        var options = WindowOptions.DefaultVulkan;
        options.Size = new(width, height);
        options.Title = "Vulkan with Silk.NET";
        options.IsEventDriven = EventBasedRendering;
        this.window = Silk.NET.Windowing.Window.Create(options);
        this.window.Initialize(); // For safety the window should be initialized before querying the VkSurface

        if (this.window.VkSurface is null)
            throw new NotSupportedException("Windowing platform doesn't support Vulkan.");

        this.window.FramebufferResize += OnFramebufferResize;
    }

    private void OnFramebufferResize(Vector2D<int> obj)
    {
        //this.framebufferResized = true;
        this.HandleBadFrameBuffer();
        this.OnResetRenderer?.Invoke(this, new());
        this.window.DoRender();
    }

    public event EventHandler? OnResetRenderer;

    public void HandleBadFrameBuffer()
    {
        var framebufferSize = this.window.FramebufferSize;

        while (framebufferSize.X == 0 || framebufferSize.Y == 0)
        {
            framebufferSize = this.window.FramebufferSize;
            this.window.DoEvents();
        }
    }
}