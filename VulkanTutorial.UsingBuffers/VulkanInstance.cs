using System.Reflection;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;

namespace VulkanTutorial.UsingBuffers;

public sealed class VulkanInstance : VulkanDependancy, IDisposable
{
    private readonly Instance instance;
    public Instance Instance => instance;

#if VULKAN_VALIDATION
    private readonly string[] validationLayers;
    public IReadOnlyList<string> ValidationLayers => this.validationLayers;
    private static readonly string[][] validationLayerNamesPriorityList =
    {
        new [] { "VK_LAYER_KHRONOS_validation" },
        new [] { "VK_LAYER_LUNARG_standard_validation" },
        new []
        {
            "VK_LAYER_GOOGLE_threading",
            "VK_LAYER_LUNARG_parameter_validation",
            "VK_LAYER_LUNARG_object_tracker",
            "VK_LAYER_LUNARG_core_validation",
            "VK_LAYER_GOOGLE_unique_objects",
        }
    };
#endif
    private static readonly string[] instanceExtensions = { ExtDebugUtils.ExtensionName };

    public VulkanInstance(Vk vk, IVkSurfaceSource surfaceSource) : base(vk)
    {
#if VULKAN_VALIDATION
        this.validationLayers = this.OptimalValidationLayers ??
                                throw new NotSupportedException("Validation layers requested, but not available!");
#endif

        unsafe
        {
            //uint extensionCount;
            //vk.EnumerateInstanceExtensionProperties((byte*) null, &extensionCount, null);
            //var extensionProperties = stackalloc ExtensionProperties[(int) extensionCount];
            //vk.EnumerateInstanceExtensionProperties((byte*) null, &extensionCount, extensionProperties);
            //Console.WriteLine("Available extensions:");
            //for (var i = 0; i < extensionCount; i++)
            //    Console.WriteLine("\t" + Marshal.PtrToStringAnsi((IntPtr) extensionProperties[i].ExtensionName));

            ApplicationInfo appInfo = new()
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = (byte*) Marshal.StringToHGlobalAnsi(Assembly.GetExecutingAssembly().FullName),
                ApplicationVersion = new Version32(1, 0, 0),
                PEngineName = (byte*) Marshal.StringToHGlobalAnsi("No Engine"),
                EngineVersion = new Version32(1, 0, 0),
                ApiVersion = Vk.Version11
            };

            InstanceCreateInfo createInfo = new()
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo
            };
            var extensions = surfaceSource.VkSurface!.GetRequiredExtensions(out var extCount);
            var newExtensions = stackalloc byte*[(int) (extCount + instanceExtensions.Length)];
            for (var i = 0; i < extCount; i++)
                newExtensions[i] = extensions[i];

            for (var i = 0; i < instanceExtensions.Length; i++)
                newExtensions[extCount + i] = (byte*) SilkMarshal.StringToPtr(instanceExtensions[i]);


            extCount += (uint) instanceExtensions.Length;
            createInfo.EnabledExtensionCount = extCount;
            createInfo.PpEnabledExtensionNames = newExtensions;

            //DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
#if VULKAN_VALIDATION
            createInfo.EnabledLayerCount = (uint)this.validationLayers.Length;
            createInfo.PpEnabledLayerNames = (byte**) SilkMarshal.StringArrayToPtr(validationLayers);
            //PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
            //createInfo.PNext = &debugCreateInfo;
#endif

            fixed (Instance* pInstance = &this.instance)
                if (vk.CreateInstance(&createInfo, null, pInstance) != Result.Success)
                    throw new VulkanException("Failed to create instance!");

            vk.CurrentInstance = this.Instance;

            Marshal.FreeHGlobal((nint) appInfo.PApplicationName);
            Marshal.FreeHGlobal((nint) appInfo.PEngineName);


#if VULKAN_VALIDATION
            SilkMarshal.Free((nint) createInfo.PpEnabledLayerNames);
#endif
        }
    }

#if VULKAN_VALIDATION
    private string[]? OptimalValidationLayers
    {
        get
        {
            string?[] availableLayerNames;
            unsafe
            {
                var layerCount = 0u;
                this.Vk.EnumerateInstanceLayerProperties(&layerCount, (LayerProperties*) 0);

                var availableLayers = new LayerProperties[layerCount];
                fixed (LayerProperties* availableLayersPtr = availableLayers)
                    this.Vk.EnumerateInstanceLayerProperties(&layerCount, availableLayersPtr);

                availableLayerNames = availableLayers
                    .Select(availableLayer => Marshal.PtrToStringAnsi((nint) availableLayer.LayerName)).ToArray();
            }

            return validationLayerNamesPriorityList
                .FirstOrDefault(validationLayerNameSet =>
                    validationLayerNameSet.All(validationLayerName =>
                        availableLayerNames.Contains(validationLayerName)));
        }
    }


#endif

    public void Dispose()
    {
        unsafe
        {
            this.Vk.DestroyInstance(this.Instance, null);
        }
    }
}