using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace VulkanTutorial.UniformBuffers;

public sealed class VulkanSyncObjects : VulkanDeviceDependancy, IDisposable
{
    public const int MaxFramesInFlight = 8;
    private readonly Semaphore[] imageAvailableSemaphores;
    private readonly Semaphore[] renderFinishedSemaphores;
    private readonly Fence[] inFlightFences;

    public VulkanSyncObjects(Vk vk, VulkanVirtualDevice device) : base(vk, device)
    {
        imageAvailableSemaphores = new Semaphore[MaxFramesInFlight];
        renderFinishedSemaphores = new Semaphore[MaxFramesInFlight];
        inFlightFences = new Fence[MaxFramesInFlight];

        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo
        };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.FenceCreateSignaledBit
        };

        for (var i = 0; i < MaxFramesInFlight; i++)
        {
            Semaphore imgAvSema, renderFinSema;
            Fence inFlightFence;
            unsafe
            {
                if (vk.CreateSemaphore(device.Device, &semaphoreInfo, null, &imgAvSema) != Result.Success ||
                    vk.CreateSemaphore(device.Device, &semaphoreInfo, null, &renderFinSema) != Result.Success ||
                    vk.CreateFence(device.Device, &fenceInfo, null, &inFlightFence) != Result.Success)
                    throw new VulkanException("failed to create synchronization objects for a frame!");
            }

            imageAvailableSemaphores[i] = imgAvSema;
            renderFinishedSemaphores[i] = renderFinSema;
            inFlightFences[i] = inFlightFence;
        }
    }

    public (Semaphore imageAvailableSemaphore, Semaphore renderFinishedSemaphore, Fence inFlightFence) this[int frame] => (this.imageAvailableSemaphores[frame], this.renderFinishedSemaphores[frame], this.inFlightFences[frame]);

    public void Dispose()
    {
        unsafe
        {
            for (var i = 0; i < MaxFramesInFlight; i++)
            {
                this.Vk.DestroySemaphore(this.Device.Device, renderFinishedSemaphores[i], null);
                this.Vk.DestroySemaphore(this.Device.Device, imageAvailableSemaphores[i], null);
                this.Vk.DestroyFence(this.Device.Device, inFlightFences[i], null);
            }
        }
    }
}