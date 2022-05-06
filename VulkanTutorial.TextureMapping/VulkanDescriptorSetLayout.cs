using Silk.NET.Vulkan;

namespace VulkanTutorial.TextureMapping;

public sealed class VulkanDescriptorSetLayout : VulkanDeviceDependancy, IDisposable
{

    public readonly DescriptorSetLayout Layout;

    public VulkanDescriptorSetLayout(Vk vk, VulkanVirtualDevice device) : base(vk, device)
    {
        unsafe
        {
            DescriptorSetLayoutBinding layoutBinding = new(0, DescriptorType.UniformBuffer, descriptorCount: 1, stageFlags: ShaderStageFlags.ShaderStageVertexBit);
            DescriptorSetLayoutCreateInfo createInfo = new(bindingCount: 1, pBindings: &layoutBinding);
            fixed (DescriptorSetLayout* pLayout = &this.Layout)
                if (this.Vk.CreateDescriptorSetLayout(this.Device.Device, in createInfo, null, pLayout) != Result.Success)
                    throw new VulkanException("failed to create descriptor set layout!");
        }
    }

    public void Dispose()
    {
        unsafe
        {
            this.Vk.DestroyDescriptorSetLayout(this.Device.Device, this.Layout, null);
        }
    }
}
