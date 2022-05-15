using Silk.NET.Vulkan;

namespace VulkanTutorial.Multisampling;

public sealed class VulkanTextureSampler : VulkanDeviceDependancy, IDisposable
{
    public readonly Sampler Sampler;

    public VulkanTextureSampler(Vk vk, VulkanVirtualDevice device, float maxLod) : base(vk, device)
    {
        unsafe
        {
            vk.GetPhysicalDeviceProperties(this.Device.PhysicalDevice.PhysicalDevice, out var deviceProperties);
            SamplerCreateInfo samplerInfo = new(
                magFilter: Filter.Linear,
                minFilter: Filter.Linear,
                addressModeU: SamplerAddressMode.Repeat,
                addressModeV: SamplerAddressMode.Repeat,
                addressModeW: SamplerAddressMode.Repeat,
                anisotropyEnable: true, // if device support
                maxAnisotropy: deviceProperties.Limits.MaxSamplerAnisotropy, // if device support else 1
                borderColor: BorderColor.IntOpaqueBlack,
                unnormalizedCoordinates: false,
                compareEnable: false,
                compareOp: CompareOp.Always,
                mipmapMode: SamplerMipmapMode.Linear,
                mipLodBias: 0,
                minLod: 0,
                maxLod: maxLod
                );
            fixed (Sampler* pSampler = &this.Sampler)
                if (vk.CreateSampler(device.Device, in samplerInfo, null, pSampler) != Result.Success)
                    throw new VulkanException("failed to create texture sampler!");
        }
    }

    public void Dispose()
    {
        unsafe
        {
            this.Vk.DestroySampler(this.Device.Device, this.Sampler, null);
        }
    }
}
