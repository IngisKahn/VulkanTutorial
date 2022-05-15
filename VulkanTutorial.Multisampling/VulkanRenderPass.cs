using Silk.NET.Vulkan;

namespace VulkanTutorial.Multisampling;

public sealed class VulkanRenderPass : VulkanDeviceDependancy, IDisposable
{
    private readonly RenderPass renderPass;
    public RenderPass RenderPass => this.renderPass;

    public VulkanRenderPass(Vk vk, VulkanVirtualDevice device, VulkanSwapChain swapChain, SampleCountFlags sampleCount) : base(vk, device)
    {
        AttachmentDescription colorAttachment = new()
        {
            Format = swapChain.SwapchainImageFormat,
            Samples = sampleCount,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.ColorAttachmentOptimal
        };
        AttachmentDescription colorAttachmentResolve = new()
        {
            Format = swapChain.SwapchainImageFormat,
            Samples = SampleCountFlags.SampleCount1Bit,
            LoadOp = AttachmentLoadOp.DontCare,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };
        AttachmentDescription depthAttachment = new()
        {
            Format = device.PhysicalDevice.DepthFormat,
            Samples = sampleCount,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
        };

        AttachmentReference colorAttachmentRef = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };
        AttachmentReference depthAttachmentRef = new()
        {
            Attachment = 1,
            Layout = ImageLayout.DepthStencilAttachmentOptimal
        };
        AttachmentReference colorAttachmentResolveRef = new()
        {
            Attachment = 2,
            Layout = ImageLayout.ColorAttachmentOptimal
        };

        unsafe
        {
            SubpassDescription subpass = new()
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = 1,
                PColorAttachments = &colorAttachmentRef,
                PDepthStencilAttachment = &depthAttachmentRef,
                PResolveAttachments = &colorAttachmentResolveRef
            };

            SubpassDependency dependency = new()
            {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit | PipelineStageFlags.PipelineStageEarlyFragmentTestsBit,
                SrcAccessMask = 0,
                DstStageMask = PipelineStageFlags.PipelineStageColorAttachmentOutputBit | PipelineStageFlags.PipelineStageEarlyFragmentTestsBit,
                DstAccessMask = AccessFlags.AccessColorAttachmentReadBit | AccessFlags.AccessColorAttachmentWriteBit | AccessFlags.AccessDepthStencilAttachmentWriteBit
            };

            var attachments = stackalloc AttachmentDescription[3] { colorAttachment, depthAttachment, colorAttachmentResolve };
            RenderPassCreateInfo renderPassInfo = new(
                attachmentCount: 3,
                pAttachments: attachments,
                subpassCount: 1,
                pSubpasses: &subpass,
                dependencyCount: 1,
                pDependencies: &dependency
            );

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
