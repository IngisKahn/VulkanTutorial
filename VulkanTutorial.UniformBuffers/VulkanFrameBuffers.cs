using Silk.NET.Vulkan;

namespace VulkanTutorial.UniformBuffers;

public sealed class VulkanFrameBuffers : VulkanDeviceDependancy, IDisposable
{
    private readonly Framebuffer[] framebuffers;
    public int Length => this.framebuffers.Length;
    public Framebuffer this[int i] => this.framebuffers[i];

    public VulkanFrameBuffers(Vk vk, VulkanVirtualDevice device, VulkanSwapChain swapChain) : base(vk, device)
    {
        this.framebuffers = new Framebuffer[swapChain.ImageViews.Length];

        for (var i = 0; i < swapChain.ImageViews.Length; i++)
        {
            var attachment = swapChain.ImageViews[i];
            Framebuffer framebuffer = new();
            unsafe
            {
                var framebufferInfo = new FramebufferCreateInfo
                {
                    SType = StructureType.FramebufferCreateInfo,
                    RenderPass = swapChain.RenderPass.RenderPass,
                    AttachmentCount = 1,
                    PAttachments = &attachment,
                    Width = swapChain.SwapchainExtent.Width,
                    Height = swapChain.SwapchainExtent.Height,
                    Layers = 1
                };

                if (vk.CreateFramebuffer(device.Device, &framebufferInfo, null, &framebuffer) != Result.Success)
                    throw new("failed to create framebuffer!");
            }

            this.framebuffers[i] = framebuffer;
        }
    }

    public void Dispose()
    {
        unsafe
        {
            foreach (var framebuffer in this.framebuffers)
                this.Vk.DestroyFramebuffer(this.Device.Device, framebuffer, null);
        }
    }
}