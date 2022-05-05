using Silk.NET.Vulkan;

namespace VulkanTutorial.UniformBuffers;

public sealed class VulkanRenderPass : VulkanDeviceDependancy, IDisposable
{
    private readonly RenderPass renderPass;
    public RenderPass RenderPass => this.renderPass;

    public VulkanRenderPass(Vk vk, VulkanVirtualDevice device, VulkanSwapChain swapChain) : base(vk, device)
    {
        AttachmentDescription colorAttachment = new()
        {
            Format = swapChain.SwapchainImageFormat,
            Samples = SampleCountFlags.SampleCount1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        AttachmentReference colorAttachmentRef = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };

        unsafe
        {
            SubpassDescription subpass = new()
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorAttachmentRef
            };

            SubpassDependency dependency = new()
            {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit,
                SrcAccessMask = 0,
                DstStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit,
                DstAccessMask = AccessFlags.AccessColorAttachmentReadBit | AccessFlags.AccessColorAttachmentWriteBit
            };

            RenderPassCreateInfo renderPassInfo = new()
            {
                SType = StructureType.RenderPassCreateInfo,
                AttachmentCount = 1,
                PAttachments = &colorAttachment,
                SubpassCount = 1,
                PSubpasses = &subpass,
                DependencyCount = 1,
                PDependencies = &dependency
            };

            fixed (RenderPass* pRenderPass = &this.renderPass)
                if (vk.CreateRenderPass(device.Device, &renderPassInfo, null, pRenderPass) != Result.Success)
                    throw new("failed to create render pass!");
        }
    }

    public void Dispose()
    {
        unsafe
        {
            this.Vk.DestroyRenderPass(this.Device.Device, this.renderPass, null);
        }
    }
}
