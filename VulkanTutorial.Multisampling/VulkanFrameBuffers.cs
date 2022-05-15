using Silk.NET.Vulkan;

namespace VulkanTutorial.Multisampling;

public sealed class VulkanFrameBuffers : VulkanDeviceDependancy, IDisposable
{
    private readonly Framebuffer[] framebuffers;
    public int Length => this.framebuffers.Length;
    public Framebuffer this[int i] => this.framebuffers[i];

    public VulkanFrameBuffers(Vk vk, VulkanVirtualDevice device, VulkanSwapChain swapChain, VulkanImageView depthImageView, VulkanImageView colorImageView) : base(vk, device)
    {
        this.framebuffers = new Framebuffer[swapChain.ImageViews.Length];

        unsafe
        {
            var attachments = stackalloc ImageView[3];
            attachments[1] = depthImageView.ImageView;
            attachments[0] = colorImageView.ImageView;
            for (var i = 0; i < swapChain.ImageViews.Length; i++)
            {
                attachments[2] = swapChain.ImageViews[i].ImageView;
                Framebuffer framebuffer = new();
                FramebufferCreateInfo framebufferInfo = new(
                    renderPass: swapChain.RenderPass.RenderPass,
                    attachmentCount: 3,
                    pAttachments: attachments,
                    width: swapChain.SwapchainExtent.Width,
                    height: swapChain.SwapchainExtent.Height,
                    layers: 1
                );

                if (vk.CreateFramebuffer(device.Device, &framebufferInfo, null, &framebuffer) != Result.Success)
                    throw new("failed to create framebuffer!");

                this.framebuffers[i] = framebuffer;
            }
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