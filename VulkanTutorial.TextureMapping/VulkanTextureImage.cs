﻿using Silk.NET.Vulkan;
using SixLabors.ImageSharp.PixelFormats;
using RawImage = SixLabors.ImageSharp.Image;
using Image = Silk.NET.Vulkan.Image;
using System.Runtime.InteropServices;

namespace VulkanTutorial.TextureMapping;

public sealed class VulkanTextureImage : VulkanDeviceDependancy, IDisposable
{
    public VulkanImage? Image { get; private set; }

    public VulkanTextureImage(Vk vk, VulkanVirtualDevice device) : base(vk, device) { }

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
    }

    public void Dispose() => this.Image?.Dispose();

    public static async Task<VulkanTextureImage> Load(Vk vk, VulkanVirtualDevice device, Stream imageStream, VulkanCommandPool commandPool)
    {
        VulkanTextureImage textureImage = new(vk, device);
        await textureImage.Initialize(imageStream, commandPool);
        return textureImage;
    }
}

public class VulkanImage : VulkanDeviceDependancy, IDisposable
{
    public Image Image;
    public DeviceMemory Memory;

    public VulkanImage(Vk vk, VulkanVirtualDevice device, uint width, uint height, Format format, ImageTiling imageTiling, ImageUsageFlags imageUsage, MemoryPropertyFlags memoryProperty) : base(vk, device)
    {
        unsafe
        {
            ImageCreateInfo imageInfo = new(
                imageType: ImageType.ImageType2D,
                extent: new(width, height, 1u),
                mipLevels: 1,
                arrayLayers: 1,
                format: format,
                tiling: imageTiling,
                initialLayout: ImageLayout.Undefined,
                usage: imageUsage,
                sharingMode: SharingMode.Exclusive,
                samples: SampleCountFlags.SampleCount1Bit);

            fixed (Image* pImage = &this.Image)
                if (this.Vk.CreateImage(this.Device.Device, in imageInfo, null, pImage) != Result.Success)
                    throw new VulkanException("failed to create image!");
            this.Vk.GetImageMemoryRequirements(this.Device.Device, this.Image, out var memoryRequirements);

            MemoryAllocateInfo allocateInfo = new(allocationSize: memoryRequirements.Size, memoryTypeIndex: device.PhysicalDevice.FindMemoryType(memoryRequirements.MemoryTypeBits, MemoryPropertyFlags.MemoryPropertyDeviceLocalBit));
            fixed (DeviceMemory* pDeviceMemory = &this.Memory)
                if (this.Vk.AllocateMemory(this.Device.Device, in allocateInfo, null, pDeviceMemory) != Result.Success)
                    throw new VulkanException("failed to allocate image memory!");
            this.Vk.BindImageMemory(this.Device.Device, this.Image, this.Memory, 0);
        }
    }

    public void Dispose()
    {
        unsafe
        {
            this.Vk.DestroyImage(this.Device.Device, this.Image, null);
            this.Vk.FreeMemory(this.Device.Device, this.Memory, null);
        }
    }

    public void TransitionImageLayout(VulkanCommandPool commandPool, Format format, ImageLayout oldLayout, ImageLayout newLayout)
    {
        using VulkanCommandBuffer commandBuffer = new(this.Vk, this.Device, commandPool.CommandPool);

        unsafe
        {
            ImageMemoryBarrier barrier = new(
                oldLayout: oldLayout, 
                newLayout: newLayout, 
                srcQueueFamilyIndex: Vk.QueueFamilyIgnored, 
                dstQueueFamilyIndex: Vk.QueueFamilyIgnored,
                image: this.Image,
                subresourceRange: new(ImageAspectFlags.ImageAspectColorBit, 0, 1, 0, 1),
                );

            PipelineStageFlags sourceStage, destinationStage;
            if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
            {
                barrier.SrcAccessMask = 0;
                barrier.DstAccessMask = AccessFlags.AccessTransferWriteBit;
                sourceStage = PipelineStageFlags.PipelineStageTopOfPipeBit;
                destinationStage = PipelineStageFlags.PipelineStageTransferBit;
            }
            else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
            {
                barrier.SrcAccessMask = AccessFlags.AccessTransferWriteBit;
                barrier.DstAccessMask = AccessFlags.AccessShaderReadBit;
                sourceStage = PipelineStageFlags.PipelineStageTransferBit;
                destinationStage = PipelineStageFlags.PipelineStageFragmentShaderBit;
            }
            else
                throw new VulkanException("unsuported layout transition!")

            this.Vk.CmdPipelineBarrier(commandBuffer.Buffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, in barrier);
        }
    }

    public void CopyBufferToImage(in Silk.NET.Vulkan.Buffer buffer, in CommandPool commandPool, uint width, uint height)
    {
        using VulkanCommandBuffer commandBuffer = new(this.Vk, this.Device, commandPool);
        BufferImageCopy region = new(0, 0, 0, new(ImageAspectFlags.ImageAspectColorBit, 0, 0, 1), new(0, 0, 0), new(width, height, 1));
        unsafe
        {
            this.Vk.CmdCopyBufferToImage(commandBuffer.Buffer, buffer, this.Image, ImageLayout.TransferDstOptimal, 1, &region);
        }
    }
}
