using Silk.NET.Vulkan;

namespace VulkanTutorial.DepthBuffering;

public sealed class VulkanDescriptorSetLayout : VulkanDeviceDependancy, IDisposable
{

    public readonly DescriptorSetLayout Layout;

    public VulkanDescriptorSetLayout(Vk vk, VulkanVirtualDevice device) : base(vk, device)
    {
        unsafe
        {
            DescriptorSetLayoutBinding layoutBinding = new(0, DescriptorType.UniformBuffer, descriptorCount: 1, stageFlags: ShaderStageFlags.ShaderStageVertexBit);

            DescriptorSetLayoutBinding samplerLayoutBinding = new(1, DescriptorType.CombinedImageSampler, 1, ShaderStageFlags.ShaderStageFragmentBit);

            var pBindings = stackalloc DescriptorSetLayoutBinding[2] { layoutBinding, samplerLayoutBinding };

            DescriptorSetLayoutCreateInfo createInfo = new(bindingCount: 2, pBindings: pBindings);
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
