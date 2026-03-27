using eft_dma_radar;
using eft_dma_radar.Tarkov.EFTPlayer;
using eft_dma_radar.Tarkov.Features;
using eft_dma_shared.Common.DMA.ScatterAPI;
using eft_dma_shared.Common.Features;
using eft_dma_shared.Common.Misc;
using eft_dma_shared.Common.Unity;
using eft_dma_shared.Common.Unity.LowLevel;
using eft_dma_shared.Common.Unity.LowLevel.Hooks;
using Reloaded.Assembler;
using System;
using System.Text;
using static eft_dma_shared.Common.Unity.MonoLib;
using static eft_dma_shared.Common.Unity.UnityOffsets;

namespace eft_dma_radar.Tarkov.Features.MemoryWrites.Patches
{
    public sealed class StreamerMode : MemPatchFeature<StreamerMode>
    {
        private bool _set;

        public override bool Enabled
        {
            get => MemWrites.Config.StreamerMode;
            set
            {
                if (MemWrites.Config.StreamerMode == value) return; 
                if (MemWrites.Config.AdvancedMemWrites is false) return; // Only allow if advanced memory writes are enabled
                MemWrites.Config.StreamerMode = value;

                if (value)
                {
                    TryApply();
                }
            }
        }

        public override bool TryApply()
        {
            if (_set) return true;

            try
            {
                if (!Enabled) return false;

                LoneLogging.WriteLine("StreamerMode: Applying patches...");
                SpoofName();
                PatchIsLocalStreamer();
                //DebugDogtagMethods();
                PatchDogtagNicknameP1();
                PatchDogtagNicknameP2();
                GloballySpoofLevel();

                DisableExperiencePanels();
                DisableStatsPanel();
                DisableRightSide();
                DisableNotifier();

                LoneLogging.WriteLine("StreamerMode: Applied Successfully!");
                _set = true;
            }
            catch (Exception ex)
            {
                LoneLogging.WriteLine($"ERROR configuring StreamerMode: {ex}");
                return false;
            }
            return true;
        }

        private void DisableExperiencePanels()
        {
            LoneLogging.WriteLine("Disabling Experience Panels...");

            ulong playerModelViewExperiencePanel = NativeMethods.FindGameObjectS("Common UI/Common UI/InventoryScreen/Overall Panel/LeftSide/CharacterPanel/PlayerModelView/BottomField/Experience");
            if (playerModelViewExperiencePanel != 0x0)
            {
                NativeMethods.GameObjectSetActive(playerModelViewExperiencePanel, false);
                LoneLogging.WriteLine("PlayerModelView Experience Panel disabled!");
            }
            else
            {
                LoneLogging.WriteLine("Failed to find PlayerModelView Experience Panel.");
            }

            ulong progressPanelCurrentText = NativeMethods.FindGameObjectS("Common UI/Common UI/InventoryScreen/SkillsAndMasteringPanel/TopPanel/Progress Panel/Current Text");
            if (progressPanelCurrentText != 0x0)
            {
                NativeMethods.GameObjectSetActive(progressPanelCurrentText, false);
                LoneLogging.WriteLine("Progress Panel Current Text disabled!");
            }
            else
            {
                LoneLogging.WriteLine("Failed to find Progress Panel Current Text.");
            }

            ulong progressPanelRemainingText = NativeMethods.FindGameObjectS("Common UI/Common UI/InventoryScreen/SkillsAndMasteringPanel/TopPanel/Progress Panel/Remaining Text");
            if (progressPanelRemainingText != 0x0)
            {
                NativeMethods.GameObjectSetActive(progressPanelRemainingText, false);
                LoneLogging.WriteLine("Progress Panel Remaining Text disabled!");
            }
            else
            {
                LoneLogging.WriteLine("Failed to find Progress Panel Remaining Text.");
            }

            ulong progressPanelBarExperienceText = NativeMethods.FindGameObjectS("Common UI/Common UI/InventoryScreen/SkillsAndMasteringPanel/TopPanel/Progress Panel/Bar/New Glow/Experience");
            if (progressPanelBarExperienceText != 0x0)
            {
                NativeMethods.GameObjectSetActive(progressPanelBarExperienceText, false);
                LoneLogging.WriteLine("Progress Panel Bar Experience Text disabled!");
            }
            else
            {
                LoneLogging.WriteLine("Failed to find Progress Panel Bar Experience Text.");
            }
        }
        private void DisableStatsPanel()
        {
            LoneLogging.WriteLine("Disabling Stats Panel...");
            ulong statsPanel = NativeMethods.FindGameObjectS("Common UI/Common UI/InventoryScreen/Overall Panel/LeftSide/CharacterPanel/Level Panel/Stats");
            if (statsPanel != 0x0)
            {
                NativeMethods.GameObjectSetActive(statsPanel, false);
                LoneLogging.WriteLine("Stats Panel disabled!");
            }
            else
            {
                LoneLogging.WriteLine("Failed to find Stats Panel.");
            }
        }
        private void DisableRightSide()
        {
            LoneLogging.WriteLine("Disabling RightSide Panel...");
            ulong rightSide = NativeMethods.FindGameObjectS("Common UI/Common UI/InventoryScreen/Overall Panel/RightSide");
            if (rightSide != 0x0)
            {
                NativeMethods.GameObjectSetActive(rightSide, false);
                LoneLogging.WriteLine("RightSide Panel disabled!");
            }
            else
            {
                LoneLogging.WriteLine("Failed to find RightSide Panel.");
            }
        }

        private void DisableNotifier()
        {
            LoneLogging.WriteLine("Disabling Notifier...");
            ulong notifier = NativeMethods.FindGameObjectS("Preloader UI/Preloader UI/BottomPanel/Content/UpperPart/Notifier/Content");
            if (notifier != 0x0)
            {
                NativeMethods.GameObjectSetActive(notifier, false);
                LoneLogging.WriteLine("Notifier disabled!");
            }
            else
            {
                LoneLogging.WriteLine("Failed to find Notifier.");
            }
        }

        private void SpoofName()
        {
            if (!(Memory.LocalPlayer is LocalPlayer localPlayer))
                return;

            var profile = Memory.ReadPtr(localPlayer + Offsets.Player.Profile);
            var profileInfo = Memory.ReadPtr(profile + Offsets.Profile.Info);

            ulong usernameAddr = Memory.ReadPtr(profileInfo + Offsets.PlayerInfo.Nickname); // Username
            int originalUsernameLength = Memory.ReadValue<int>(usernameAddr + UnityOffsets.UnityString.Length);

            LoneLogging.WriteLine($"Original Username Length: {originalUsernameLength}, Username Address: {usernameAddr}");

            using (var scatterWrite = new ScatterWriteHandle())
            {
                string spoofedName = new string(' ', originalUsernameLength);
                scatterWrite.AddBufferEntry<byte>(usernameAddr + UnityOffsets.UnityString.Value, Encoding.Unicode.GetBytes(spoofedName));        
                scatterWrite.AddValueEntry(profileInfo + Offsets.PlayerInfo.MemberCategory, (int)Enums.EMemberCategory.Sherpa);
                scatterWrite.Execute(() => true);
            }
        }

        private bool IsLocalStreamerMethodPatched = false;
        /// <summary>
        /// Force "<Streamer>" text for names.
        /// </summary>
        private void PatchIsLocalStreamer()
        {
            if (IsLocalStreamerMethodPatched) return;

            SignatureInfo sigInfo = new(null, ShellKeeper.PatchTrue);

            PatchMethodE(ClassNames.StreamerMode.ClassName, ClassNames.StreamerMode.MethodName, sigInfo, compileClass: true);

            IsLocalStreamerMethodPatched = true;
        }

        private bool DogtagNicknamePatchedP1 = false;
        private static readonly byte[] DogtagNicknameP1Signature = new byte[]
        {
            0x48, 0x8B, 0x46, 0x30
        };

        /// <summary>
        /// Makes the function return null instead of the nickname field.
        /// </summary>
        private static readonly byte[] DogtagNicknameP1Patch = new byte[]
        {
            0x48, 0x31, 0xC0, 0x90 // xor rax, rax
        };

        private void PatchDogtagNicknameP1()
        {
            if (DogtagNicknamePatchedP1) return;

            var mClass = MonoClass.Find("Assembly-CSharp", "EFT.InventoryLogic.DogtagComponent", out ulong classAddress);
            if (classAddress == 0x0)
            {
                LoneLogging.WriteLine($"[ERROR] Class 'EFT.InventoryLogic.DogtagComponent' not found!");
                return;
            }

            // Compile the class to ensure the method address is valid
            ulong compiledClass = NativeMethods.CompileClass(classAddress);
            if (compiledClass == 0x0)
            {
                LoneLogging.WriteLine($"[ERROR] Unable to compile class 'EFT.InventoryLogic.DogtagComponent'!");
                return;
            }

            // Re-check the method after compilation
            var methodPtr = mClass.FindMethod(@"\uE000");
            if (methodPtr == 0x0)
            {
                LoneLogging.WriteLine($"[ERROR] Unable to find method '\uE000' in 'EFT.InventoryLogic.DogtagComponent' after compilation!");
                return;
            }

            LoneLogging.WriteLine($"[INFO] Found method '\uE000' at 0x{methodPtr:X}");

            // Patch the method
            SignatureInfo sigInfo = new(DogtagNicknameP1Signature, DogtagNicknameP1Patch, 200);
            PatchMethodE("EFT.InventoryLogic.DogtagComponent", @"\uE000", sigInfo, compileClass: true);

            DogtagNicknamePatchedP1 = true;
        }

        private bool DogtagNicknamePatchedP2 = false;
        private const string DogtagNicknameP2SignatureMask = "xx????xxx";
        private static readonly byte[] DogtagNicknameP2Signature = new byte[]
        {
            0x0F, 0x84, 0x0, 0x0, 0x0, 0x0,
            0x4D, 0x8B, 0x66
        };

        /// <summary>
        /// Basically tring to make it so this if statement's contents are not ran:
        /// if (itemComponent3 != null && !string.IsNullOrEmpty(itemComponent3.Nickname))
        /// {
        ///	    text = (examined? itemComponent3.Nickname.SubstringIfNecessary(20) : \uEF86.\uE000(295345));
        /// }
        /// </summary>
        private static readonly byte[] DogtagNicknameP2Patch = new byte[]
        {
            0x90, 0xE9
        };

        /// <summary>
        /// Patches game code to hide the player nickname on all dogtag item grids.
        /// </summary>
        private void PatchDogtagNicknameP2()
        {
            if (DogtagNicknamePatchedP2) 
                return;

            SignatureInfo sigInfo = new(DogtagNicknameP2Signature, DogtagNicknameP2Signature.Patch(DogtagNicknameP2Patch), 0x1000, DogtagNicknameP2SignatureMask, DogtagNicknameP2SignatureMask, 0, DogtagNicknameP2Patch);

            PatchMethodE("EFT.UI.DragAndDrop.GridItemView", ClassNames.GridItemView.MethodName, sigInfo, compileClass: true);

            DogtagNicknamePatchedP2 = true;
        }

        private void DebugDogtagMethods()
        {
            MonoClass.PrintMethods("Assembly-CSharp", "EFT.InventoryLogic.DogtagComponent");
            MonoClass.PrintMethods("Assembly-CSharp", "EFT.UI.DragAndDrop.GridItemView");
        }

        private bool LevelGloballySpoofed = false;
        private static readonly byte[] GloballySpoofLevelSignature = new byte[]
        {
            0x45, 0x85, 0xF6,
            0x0F, 0x84,
        };

        private void GloballySpoofLevel()
        {
            if (LevelGloballySpoofed) return;

            Assembler assembler = new();

            string[] mnemonicsA = new[]
            {
                "use64",
                "mov rdi, 79",
                "nop",
                "nop",
            };

            byte[] shellcodeA = assembler.Assemble(mnemonicsA);
            SignatureInfo sigInfoA = new(GloballySpoofLevelSignature, shellcodeA, 100);
            PatchMethodE("EFT.UI.PlayerLevelPanel", "Set", sigInfoA, compileClass: true);

            SignatureInfo sigInfoB = new(null, ShellKeeper.ReturnInt(79));
            PatchMethodE("EFT.Profile+TraderInfo", "get_ProfileLevel", sigInfoB, compileClass: true);

            LevelGloballySpoofed = true;
        }

        public override void OnGameStop()
        {
            base.OnGameStop();

            _set = false;
            IsLocalStreamerMethodPatched = false;
            DogtagNicknamePatchedP1 = false;
            DogtagNicknamePatchedP2 = false;
            LevelGloballySpoofed = false;
        }
    }
}
