using Silk.NET.Vulkan;

namespace VulkanTutorial.TextureMapping;

public abstract class VulkanDescriptorSetsBase : VulkanDeviceDependancy
{    
    public readonly DescriptorSet[] DescriptorSets = new DescriptorSet[VulkanSyncObjects.MaxFramesInFlight];
    
    protected VulkanDescriptorSetsBase(Vk vk, VulkanVirtualDevice device) : base(vk, device)
    {
    }
}

public sealed class VulkanDescriptorSets<T> : VulkanDescriptorSetsBase, IDisposable where T : unmanaged
{
    private readonly VulkanDescriptorPool descriptorPool;
    public VulkanDescriptorSets(Vk vk, VulkanVirtualDevice device, VulkanDescriptorSetLayout vulkanDescriptorSetLayout, VulkanUniformBuffer<T>[] uniformBuffers, VulkanTextureImage textureImage, VulkanTextureSampler sampler)
        : base(vk, device)
    {
        this.descriptorPool = new(vk, device);
        var layouts = new DescriptorSetLayout[VulkanSyncObjects.MaxFramesInFlight];
        Array.Fill(layouts, vulkanDescriptorSetLayout.Layout);
        unsafe
        {
            fixed (DescriptorSetLayout* pLayouts = layouts)
            {
                DescriptorSetAllocateInfo allocateInfo = new(descriptorPool: descriptorPool.DescriptorPool, descriptorSetCount: VulkanSyncObjects.MaxFramesInFlight, pSetLayouts: pLayouts);
                fixed (DescriptorSet* pDescriptorSets = this.DescriptorSets)
                {
                    if (this.Vk.AllocateDescriptorSets(this.Device.Device, in allocateInfo, pDescriptorSets) != Result.Success)
                        throw new VulkanException("failed to allocate descriptor sets!");

                    var descriptorWrites = stackalloc WriteDescriptorSet[2];
                        DescriptorImageInfo imageInfo = new(sampler.Sampler, textureImage.ImageView!.ImageView, ImageLayout.ShaderReadOnlyOptimal);
                    for (var i = 0; i < VulkanSyncObjects.MaxFramesInFlight; i++)
                    {
                        var uniformBuffer = uniformBuffers[i];
                        DescriptorBufferInfo bufferInfo = new(uniformBuffer.Buffer, 0, (ulong)sizeof(T));


                        descriptorWrites[0] = new(dstSet: pDescriptorSets[i], dstBinding: 0, dstArrayElement: 0, descriptorType: DescriptorType.UniformBuffer, descriptorCount: 1, pBufferInfo: &bufferInfo);
                        descriptorWrites[1] = new(dstSet: pDescriptorSets[i], dstBinding: 1, dstArrayElement: 0, descriptorType: DescriptorType.CombinedImageSampler, descriptorCount: 1, pImageInfo: &imageInfo);
                        vk.UpdateDescriptorSets(device.Device, 2, descriptorWrites, 0, null);
                    }
                }
            }
        }
    }

    public void Dispose() => this.descriptorPool.Dispose();
}
