﻿using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using SixLabors.ImageSharp;

namespace VulkanTutorial.TextureMapping;

public abstract class VulkanDeviceBuffer<T> : VulkanBuffer where T : unmanaged
{
    protected VulkanDeviceBuffer(Vk vk,
        VulkanPhysicalDevice physicalDevice,
        VulkanVirtualDevice device, VulkanCommandPool commandPool,
        T[] sourceData, BufferUsageFlags bufferUsageFlags) : base(vk, physicalDevice, device, (ulong)(Marshal.SizeOf<T>() * sourceData.Length),
            bufferUsageFlags | BufferUsageFlags.BufferUsageTransferDstBit,
            MemoryPropertyFlags.MemoryPropertyDeviceLocalBit)
    {
        unsafe
        {
            using VulkanStagingBuffer<T> staging = new(vk, physicalDevice, device, this.Size, sourceData);
            staging.CopyTo(this, commandPool);
        }
    }

    public abstract void Bind(in CommandBuffer commandBuffer);
}
