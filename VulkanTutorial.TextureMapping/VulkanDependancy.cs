using Silk.NET.Vulkan;

namespace VulkanTutorial.TextureMapping;

public abstract class VulkanDependancy
{
    protected Vk Vk { get; }
    protected VulkanDependancy(Vk vk) => this.Vk = vk;
}
