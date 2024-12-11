using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using FrooxEngine;
using Elements.Core;
using FrooxEngine.UIX;
using System.Reflection.Emit;

namespace SaveImagePrompt;

public class SaveImagePrompt : ResoniteMod
{
    public const string VERSION = "1.0.0";
    public override string Name => "SaveImagePrompt";
    public override string Author => "art0007i";
    public override string Version => VERSION;
    public override string Link => "https://github.com/art0007i/SaveImagePrompt/";

    [AutoRegisterConfigKey]
    public static ModConfigurationKey<bool> KEY_ENABLED = new("enabled", "Should the mod be enabled?", () => true);
    static ModConfiguration config;

    public override void OnEngineInit()
    {
        config = GetConfiguration();
        Harmony harmony = new Harmony("me.art0007i.SaveImagePrompt");
        harmony.PatchAll();
    }
    [HarmonyPatch(typeof(PhotoMetadata), nameof(PhotoMetadata.NotifyOfScreenshot))]
    class SaveImagePromptPatch
    {
        public static Task Proxy(Worker worker, Func<Task> task)
        {
            if (!config.GetValue(KEY_ENABLED)) return worker.StartGlobalTask(task);

            var tcs = new TaskCompletionSource<bool>();

            var __instance = worker as PhotoMetadata;
            Userspace.UserspaceWorld.RunSynchronously(() =>
            {
                var dialog = Userspace.UserspaceWorld.RootSlot.AddSlot("Save Image Prompt");

                UIBuilder uIBuilder = RadiantUI_Panel.SetupPanel(dialog, "Save Image?".AsLocaleKey(), new float2(400f, 300f));
                dialog.LocalScale *= 0.0005f;
                RadiantUI_Constants.SetupEditorStyle(uIBuilder);
                uIBuilder.VerticalLayout(4f);
                uIBuilder.Style.MinHeight = 64f;
                uIBuilder.Text($"Would you like to save the image: \"{__instance.Slot.Name}\" from world \"{__instance.World.Name}\"");
                uIBuilder.Style.MinHeight = 32f;
                uIBuilder.HorizontalLayout(4f);
                uIBuilder.Button("Save", RadiantUI_Constants.Sub.GREEN).LocalPressed += (b, e) =>
                {
                    tcs.SetResult(true);
                    dialog.Destroy();
                };
                uIBuilder.Button("Discard", RadiantUI_Constants.Sub.RED).LocalPressed += (b, e) =>
                {
                    dialog.Destroy();
                };

                dialog.PositionInFrontOfUser(float3.Backward);

                dialog.OnPrepareDestroy += (s) =>
                {
                    if (tcs.Task.IsCompleted)
                    {
                    }
                    tcs.TrySetResult(false);
                };
            });

            // Ask the user of input, if he says yes we execute the original code (by awaiting oldTask)
            // Otherwise just do nothing and exit the function
            return Task.Run(async () =>
            {
                if (await tcs.Task)
                {
                    await worker.StartGlobalTask(task);
                }
            });
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var lookFor = AccessTools.FirstMethod(typeof(Worker), (mi) =>
                mi.Name == nameof(Worker.StartGlobalTask) && !mi.IsGenericMethod);
            foreach (var code in codes)
            {
                if (code.Calls(lookFor))
                {
                    yield return new(OpCodes.Call, AccessTools.Method(typeof(SaveImagePromptPatch), nameof(SaveImagePromptPatch.Proxy)));
                }
                else
                {
                    yield return code;
                }
            }
        }
    }
}
