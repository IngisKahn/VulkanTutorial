using Silk.NET.Vulkan;
using Image = Silk.NET.Vulkan.Image;

namespace VulkanTutorial.Multisampling;

public class VulkanImage : VulkanDeviceDependancy, IDisposable
{
    public Image Image;
    public DeviceMemory Memory;

    public VulkanImage(Vk vk, VulkanVirtualDevice device, uint width, uint height, uint mipLevels, Format format, ImageTiling imageTiling, ImageUsageFlags imageUsage, MemoryPropertyFlags memoryProperty) 
        : base(vk, device)
    {
        unsafe
        {
            ImageCreateInfo imageInfo = new(
                imageType: ImageType.ImageType2D,
                extent: new(width, height, 1u),
                mipLevels: mipLevels,
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

    public void TransitionImageLayout(VulkanCommandPool commandPool, Format format, ImageLayout oldLayout, ImageLayout newLayout, uint mipLevels)
    {
        using VulkanCommandBuffer commandBuffer = new(this.Vk, this.Device, commandPool.CommandPool);

        unsafe
        {
            var aspect = newLayout == ImageLayout.DepthStencilAttachmentOptimal 
                ? (this.Device.PhysicalDevice.HasStencilComponent(format) 
                    ? ImageAspectFlags.ImageAspectDepthBit | ImageAspectFlags.ImageAspectStencilBit
                    : ImageAspectFlags.ImageAspectDepthBit)
                : ImageAspectFlags.ImageAspectColorBit;
            ImageMemoryBarrier barrier = new(
                oldLayout: oldLayout, 
                newLayout: newLayout, 
                srcQueueFamilyIndex: Vk.QueueFamilyIgnored, 
                dstQueueFamilyIndex: Vk.QueueFamilyIgnored,
                image: this.Image,
                subresourceRange: new(aspect, 0, mipLevels, 0, 1)
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
            else if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.DepthStencilAttachmentOptimal)
            {
                barrier.SrcAccessMask = 0;
                barrier.DstAccessMask = AccessFlags.AccessDepthStencilAttachmentReadBit | AccessFlags.AccessDepthStencilAttachmentWriteBit;
                sourceStage = PipelineStageFlags.PipelineStageTopOfPipeBit;
                destinationStage = PipelineStageFlags.PipelineStageEarlyFragmentTestsBit;
            }
            else
                throw new VulkanException("unsuported layout transition!");

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
