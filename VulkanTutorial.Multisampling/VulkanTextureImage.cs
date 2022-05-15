using Silk.NET.Vulkan;
using SixLabors.ImageSharp.PixelFormats;
using RawImage = SixLabors.ImageSharp.Image;
using System.Runtime.InteropServices;
using Silk.NET.Core;

namespace VulkanTutorial.Multisampling;

public sealed class VulkanTextureImage : VulkanDeviceDependancy, IDisposable
{
    public VulkanImage? Image { get; private set; }
    public VulkanImageView? ImageView { get; private set; }

    public uint MipLevels => this.mipLevels;

    private readonly uint mipLevels;

    public VulkanTextureImage(Vk vk, VulkanVirtualDevice device, VulkanCommandPool commandPool, Stream imageStream) : base(vk, device) 
    {
        using var image = RawImage.Load<Rgba32>(imageStream);
        if (image == null)
            throw new VulkanException("failed to load texture image!");
        var width = image.Width;
        var height = image.Height;
        var imageSize = width * height * 4;
        this.mipLevels = (uint)Math.Floor(Math.Log2(Math.Max(image.Width, image.Height))) + 1;
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
                this.Image = new(this.Vk, this.Device, (uint)image.Width, (uint)image.Height, this.mipLevels, Format.R8G8B8A8Srgb, ImageTiling.Optimal, ImageUsageFlags.ImageUsageTransferDstBit | ImageUsageFlags.ImageUsageTransferSrcBit | ImageUsageFlags.ImageUsageSampledBit, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit);
                this.Image.TransitionImageLayout(commandPool, Format.R8G8B8A8Srgb, ImageLayout.Undefined, ImageLayout.TransferDstOptimal, this.mipLevels);
                this.Image.CopyBufferToImage(staging.Buffer, commandPool.CommandPool, (uint)image.Width, (uint)image.Height);
                //this.Image.TransitionImageLayout(commandPool, Format.R8G8B8A8Srgb, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal, this.mipLevels);
                this.GenerateMipMaps(width, height, Format.R8G8B8A8Srgb, commandPool);
            }
            finally
            {
                staging.Dispose();
            }
        }

        this.ImageView = new(this.Vk, this.Device, this.Image, Format.R8G8B8A8Srgb, this.mipLevels);
    }

    private void GenerateMipMaps(int width, int height, Format format, VulkanCommandPool commandPool)
    {
        this.Vk.GetPhysicalDeviceFormatProperties(this.Device.PhysicalDevice.PhysicalDevice, format, out var formatProperties);
        if ((formatProperties.OptimalTilingFeatures & FormatFeatureFlags.FormatFeatureSampledImageFilterLinearBit) == 0)
            throw new VulkanException("texture image format does not support linear blitting!");

        using VulkanCommandBuffer commandBuffer = new(this.Vk, this.Device, commandPool.CommandPool);
        unsafe
        {
            ImageMemoryBarrier barrier = new(
                image: this.Image!.Image,
                srcQueueFamilyIndex: Vk.QueueFamilyIgnored,
                dstQueueFamilyIndex: Vk.QueueFamilyIgnored,
                subresourceRange: new(ImageAspectFlags.ImageAspectColorBit, null, 1, 0, 1)
                );
            for (var i = 1u; i < this.MipLevels; i++)
            {
                barrier.SubresourceRange.BaseMipLevel = i - 1;
                barrier.OldLayout = ImageLayout.TransferDstOptimal;
                barrier.NewLayout = ImageLayout.TransferSrcOptimal;
                barrier.SrcAccessMask = AccessFlags.AccessTransferWriteBit;
                barrier.DstAccessMask = AccessFlags.AccessTransferReadBit;

                this.Vk.CmdPipelineBarrier(commandBuffer.Buffer, PipelineStageFlags.PipelineStageTransferBit, PipelineStageFlags.PipelineStageTransferBit, 0, 0, null, 0, null, 1, &barrier);

                ImageBlit blit = new(new(ImageAspectFlags.ImageAspectColorBit, i - 1, 0, 1), new(ImageAspectFlags.ImageAspectColorBit, i, 0, 1))
                {
                    SrcOffsets = new() { Element0 = new(0, 0, 0), Element1 = new(width, height, 1) },
                    DstOffsets = new() { Element0 = new(0, 0, 0), Element1 = new(width > 1 ? width / 2 : 1, height > 1 ? height / 2 : 1, 1) },
                };

                this.Vk.CmdBlitImage(commandBuffer.Buffer, this.Image.Image, ImageLayout.TransferSrcOptimal, this.Image.Image, ImageLayout.TransferDstOptimal, 1, in blit, Filter.Linear);

                barrier.OldLayout = ImageLayout.TransferSrcOptimal;
                barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
                barrier.SrcAccessMask = AccessFlags.AccessTransferReadBit;
                barrier.DstAccessMask = AccessFlags.AccessShaderReadBit;

                this.Vk.CmdPipelineBarrier(commandBuffer.Buffer, PipelineStageFlags.PipelineStageTransferBit, PipelineStageFlags.PipelineStageFragmentShaderBit, 0, 0, null, 0, null, 1, &barrier);

                if (width > 1) 
                    width /= 2;
                if (height > 1) 
                    height /= 2;
            }

            barrier.SubresourceRange.BaseMipLevel = this.MipLevels - 1;
            barrier.OldLayout = ImageLayout.TransferDstOptimal;
            barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
            barrier.SrcAccessMask = AccessFlags.AccessTransferWriteBit;
            barrier.DstAccessMask = AccessFlags.AccessShaderReadBit;

            this.Vk.CmdPipelineBarrier(commandBuffer.Buffer, PipelineStageFlags.PipelineStageTransferBit, PipelineStageFlags.PipelineStageFragmentShaderBit, 0, 0, null, 0, null, 1, &barrier);
        }
    }

    public void Dispose()
    {
        this.ImageView?.Dispose();
        this.Image?.Dispose();
    }
}
