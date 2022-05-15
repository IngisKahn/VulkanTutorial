using Silk.NET.Vulkan;

namespace VulkanTutorial.Multisampling;

public sealed class VulkanDescriptorPool : VulkanDeviceDependancy, IDisposable
{
    private readonly DescriptorPool descriptorPool;
    public DescriptorPool DescriptorPool => this.descriptorPool;

    public VulkanDescriptorPool(Vk vk, VulkanVirtualDevice device) : base(vk, device)
    {
        unsafe
        {
            var pPoolSizes = stackalloc DescriptorPoolSize[2] { new(type: DescriptorType.UniformBuffer, VulkanSyncObjects.MaxFramesInFlight), new(type: DescriptorType.CombinedImageSampler, VulkanSyncObjects.MaxFramesInFlight) };
            DescriptorPoolCreateInfo poolInfo = new(poolSizeCount: 2, pPoolSizes: pPoolSizes, maxSets: VulkanSyncObjects.MaxFramesInFlight);
            fixed (DescriptorPool* pDescriptorPool = &this.descriptorPool)
            if (this.Vk.CreateDescriptorPool(this.Device.Device, in poolInfo, null, pDescriptorPool) != Result.Success)
                throw new VulkanException("failed to create descriptor pool!");
        }
    }

    public void Dispose()
    {
        unsafe
        {
            this.Vk.DestroyDescriptorPool(this.Device.Device, this.descriptorPool, null);
        }
    }
}
