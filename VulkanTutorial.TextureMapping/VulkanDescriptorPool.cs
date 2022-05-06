using Silk.NET.Vulkan;

namespace VulkanTutorial.TextureMapping;

public sealed class VulkanDescriptorPool : VulkanDeviceDependancy, IDisposable
{
    private readonly DescriptorPool descriptorPool;
    public DescriptorPool DescriptorPool => this.descriptorPool;

    public VulkanDescriptorPool(Vk vk, VulkanVirtualDevice device) : base(vk, device)
    {
        DescriptorPoolSize poolSize = new(type: DescriptorType.UniformBuffer, VulkanSyncObjects.MaxFramesInFlight);
        unsafe
        {
            DescriptorPoolCreateInfo poolInfo = new(poolSizeCount: 1, pPoolSizes: &poolSize, maxSets: VulkanSyncObjects.MaxFramesInFlight);
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
