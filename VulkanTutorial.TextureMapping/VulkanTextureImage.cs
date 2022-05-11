using Silk.NET.Vulkan;
using SixLabors.ImageSharp.PixelFormats;
using RawImage = SixLabors.ImageSharp.Image;
using System.Runtime.InteropServices;
using Silk.NET.Core;

namespace VulkanTutorial.TextureMapping;

public sealed class VulkanTextureImage : VulkanDeviceDependancy, IDisposable
{
    public VulkanImage? Image { get; private set; }
    public VulkanImageView? ImageView { get; private set; }

    private VulkanTextureImage(Vk vk, VulkanVirtualDevice device) : base(vk, device) { }

    private async Task Initialize(Stream imageStream, VulkanCommandPool commandPool)
    {
        using var image = await RawImage.LoadAsync<Rgba32>(imageStream);
        if (image == null)
            throw new VulkanException("failed to load texture image!");
        var imageSize = image.Width * image.Height * 4;
        unsafe
        {
            VulkanStagingBuffer<Rgba32>? staging = null;
            image.ProcessPixelRows(accessor =>
            {
                fixed (Rgba32* data = &MemoryMarshal.GetReference(accessor.GetRowSpan(0)))

                    staging = new(this.Vk, this.Device, (ulong)imageSize, data);
            });
            if (staging == null)
                throw new VulkanException("failed to copy texture!");
            try
            {
                this.Image = new(this.Vk, this.Device, (uint)image.Width, (uint)image.Height, Format.R8G8B8A8Srgb, ImageTiling.Optimal, ImageUsageFlags.ImageUsageTransferDstBit | ImageUsageFlags.ImageUsageSampledBit, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit);
                this.Image.TransitionImageLayout(commandPool, Format.R8G8B8A8Srgb, ImageLayout.Undefined, ImageLayout.TransferDstOptimal);
                this.Image.CopyBufferToImage(staging.Buffer, commandPool.CommandPool, (uint)image.Width, (uint)image.Height);
                this.Image.TransitionImageLayout(commandPool, Format.R8G8B8A8Srgb, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);
            }
            finally
            {
                staging.Dispose();
            }
        }

        this.ImageView = new(this.Vk, this.Device, this.Image, Format.R8G8B8A8Srgb);
    }

    public void Dispose()
    {
        this.ImageView?.Dispose();
        this.Image?.Dispose();
    }

    public static async Task<VulkanTextureImage> Load(Vk vk, VulkanVirtualDevice device, Stream imageStream, VulkanCommandPool commandPool)
    {
        VulkanTextureImage textureImage = new(vk, device);
        await textureImage.Initialize(imageStream, commandPool);
        return textureImage;
    }
}

public sealed class VulkanTextureSampler : VulkanDeviceDependancy, IDisposable
{
    public readonly Sampler Sampler;

    public VulkanTextureSampler(Vk vk, VulkanVirtualDevice device) : base(vk, device)
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
                maxLod: 0
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
