using ABI_RC.Core.InteractionSystem;
using ABI_RC.Core;
using ABI_RC.Core.Base;
using ABI_RC.Core.IO;
using ABI_RC.Core.Networking.IO.Instancing;
using ABI_RC.Core.Networking.API.Responses.DetailsV2;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Util;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Systems.Movement;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace CVRTrainer
{
    [DisallowMultipleComponent]
    class TrainerMenuComponent : MonoBehaviour
    {
        static TrainerMenuComponent _instance;

        enum MenuPage
        {
            Main,
            Player,
            World,
            VehicleSpawner,
            PropSpawner,
            AddVehicle,
            AddProp,
            RenameSpawnable
        }

        enum SpawnableFolder
        {
            Vehicles,
            Props,
            Favorites,
            Recent
        }

        const KeyCode ToggleKey = KeyCode.F4;
        const float MenuWidth = 340f;
        const float HeaderHeight = 58f;
        const float SubHeaderHeight = 24f;
        const float RowHeight = 30f;
        const float FooterHeight = 34f;
        const float Margin = 34f;
        const int SpawnableFolderCount = 4;
        const string NavigateSoundResource = "CVRTrainer.609576__cjspellsfish__oldvgmenu.wav";
        const string InputLockId = "CVRTrainerMenu";

        static readonly string[] MainItems = {
            "Player",
            "World",
            "Vehicle Spawner",
            "Prop Spawner",
            "Close Menu"
        };

        static readonly string[] PlayerItems = {
            "Respawn",
            "Save Position",
            "Teleport Saved",
            "Flight",
            "Noclip",
            "Clip Flight",
            "Back"
        };

        static readonly string[] WorldItems = {
            "Reload World",
            "Go Home",
            "Copy World ID",
            "Copy Instance ID",
            "Drop Portal",
            "Back"
        };

        static readonly string[] VehicleItems = {
            "Folder",
            "Spawnable",
            "Add GUID",
            "Paste GUID",
            "Resolve Selected",
            "Select To Spawn",
            "Spawn Vehicle",
            "Delete Last Vehicle",
            "Add Favorite",
            "Move Entry Up",
            "Move Entry Down",
            "Rename Local Label",
            "Remove Saved",
            "Back"
        };

        static readonly string[] PropItems = {
            "Delete All My Props",
            "Delete All Props (Local)",
            "Prop Delete Mode",
            "Clear Prop Mode",
            "Back"
        };

        static readonly string[] AddItems = {
            "GUID",
            "Save",
            "Paste Clipboard",
            "Back"
        };

        static readonly string[] RenameItems = {
            "Label",
            "Save",
            "Clear",
            "Back"
        };

        MenuPage _page = MenuPage.Main;
        bool _isOpen;
        int _selectedIndex;
        SpawnableFolder _spawnableFolder;
        int _vehicleIndex;
        int _propIndex;
        int _favoriteIndex;
        int _recentIndex;
        Vector3 _savedPosition;
        Vector3 _savedEulerRotation;
        bool _hasSavedPosition;
        string _lastVehicleInstanceId = string.Empty;
        string _lastPropInstanceId = string.Empty;
        string _guidInput = string.Empty;
        string _renameInput = string.Empty;
        bool _textFieldActive;
        bool _textFieldFocusPending;
        string _status = "Ready";
        float _statusUntil;

        Texture2D _headerTexture;
        Texture2D _subHeaderTexture;
        Texture2D _bodyTexture;
        Texture2D _rowTexture;
        Texture2D _selectedTexture;
        Texture2D _accentTexture;
        Texture2D _footerTexture;
        GUIStyle _titleStyle;
        GUIStyle _subTitleStyle;
        GUIStyle _rowStyle;
        GUIStyle _selectedRowStyle;
        GUIStyle _valueStyle;
        GUIStyle _selectedValueStyle;
        GUIStyle _footerStyle;
        GUIStyle _textFieldStyle;
        Rect _lastMenuRect;
        AudioSource _audioSource;
        AudioClip _navigateClip;
        float _lastSoundTime;
        bool _gameInputLocked;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                UnityEngine.Object.Destroy(gameObject);
                return;
            }

            _instance = this;
            Object.DontDestroyOnLoad(gameObject);
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
            _audioSource.volume = 0.65f;
            _navigateClip = LoadEmbeddedWav(NavigateSoundResource);
            StartCoroutine(ResolveSavedEntries());
        }

        void OnDestroy()
        {
            SetGameInputLocked(false);

            if (_instance == this)
                _instance = null;

            DestroyTexture(_headerTexture);
            DestroyTexture(_subHeaderTexture);
            DestroyTexture(_bodyTexture);
            DestroyTexture(_rowTexture);
            DestroyTexture(_selectedTexture);
            DestroyTexture(_accentTexture);
            DestroyTexture(_footerTexture);
            if (_navigateClip != null)
                UnityEngine.Object.Destroy(_navigateClip);
        }

        void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                SetMenuOpen(!_isOpen);
                return;
            }

            if (!_isOpen)
                return;

            if (_textFieldActive)
            {
                if (Pressed(KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Keypad5))
                {
                    EndTextEntry();
                    _selectedIndex = 1;
                    ActivateSelected();
                }
                else if (Pressed(KeyCode.Escape, KeyCode.Keypad0))
                {
                    EndTextEntry();
                }
                // Backspace is consumed by the text field, so don't trigger Back().
            }
            else if (Pressed(KeyCode.K, KeyCode.DownArrow, KeyCode.Keypad2))
                MoveSelection(1);
            else if (Pressed(KeyCode.I, KeyCode.UpArrow, KeyCode.Keypad8))
                MoveSelection(-1);
            else if (Pressed(KeyCode.J, KeyCode.LeftArrow, KeyCode.Keypad4))
                AdjustOption(-1);
            else if (Pressed(KeyCode.L, KeyCode.RightArrow, KeyCode.Keypad6))
                AdjustOption(1);
            else if (Pressed(KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Keypad5, KeyCode.U))
            {
                bool isTextRow = (IsAddPage() || _page == MenuPage.RenameSpawnable) && _selectedIndex == 0;
                if (isTextRow)
                    BeginTextEntry();
                else
                    ActivateSelected();
            }
            else if (Pressed(KeyCode.Backspace, KeyCode.O, KeyCode.Escape, KeyCode.Keypad0))
                Back();

            ClearGameInputState();
        }

        void OnGUI()
        {
            if (!_isOpen)
                return;

            if (_titleStyle == null)
                CreateStyles();

            string[] items = GetCurrentItems();
            float bodyHeight = (items.Length * RowHeight) + FooterHeight;
            float x = Mathf.Max(Margin, Screen.width - MenuWidth - Margin);
            float y = Margin;
            _lastMenuRect = new Rect(x, y, MenuWidth, HeaderHeight + SubHeaderHeight + bodyHeight);

            var headerRect = new Rect(x, y, MenuWidth, HeaderHeight);
            var accentRect = new Rect(x, y + HeaderHeight - 4f, MenuWidth, 4f);
            var subHeaderRect = new Rect(x, y + HeaderHeight, MenuWidth, SubHeaderHeight);
            var bodyRect = new Rect(x, y + HeaderHeight + SubHeaderHeight, MenuWidth, bodyHeight);

            GUI.DrawTexture(headerRect, _headerTexture);
            GUI.DrawTexture(accentRect, _accentTexture);
            GUI.DrawTexture(subHeaderRect, _subHeaderTexture);
            GUI.DrawTexture(bodyRect, _bodyTexture);
            GUI.Label(headerRect, "MENU", _titleStyle);
            GUI.Label(subHeaderRect, GetPageTitle(), _subTitleStyle);

            float rowY = bodyRect.y;
            for (int i = 0; i < items.Length; i++)
            {
                var rowRect = new Rect(x, rowY + (i * RowHeight), MenuWidth, RowHeight);
                HandleMouseRow(rowRect, i);

                if ((IsAddPage() || _page == MenuPage.RenameSpawnable) && i == 0)
                    DrawTextInputRow(x, rowRect.y, items[i], _page == MenuPage.RenameSpawnable, i == _selectedIndex);
                else
                    DrawRow(x, rowRect.y, items[i], GetItemValue(i), i == _selectedIndex);
            }

            HandleMouseWheel();

            var footerRect = new Rect(x, bodyRect.y + (items.Length * RowHeight), MenuWidth, FooterHeight);
            GUI.DrawTexture(footerRect, _footerTexture);
            GUI.Label(footerRect, GetFooterText(), _footerStyle);
        }

        void DrawTextInputRow(float x, float y, string label, bool rename, bool selected)
        {
            var rowRect = new Rect(x, y, MenuWidth, RowHeight);
            GUI.DrawTexture(rowRect, selected ? _selectedTexture : _rowTexture);

            var labelRect = new Rect(x + 12f, y, 54f, RowHeight);
            GUI.Label(labelRect, label, selected ? _selectedRowStyle : _rowStyle);

            var inputRect = new Rect(x + 68f, y + 4f, MenuWidth - 80f, RowHeight - 8f);
            if (_textFieldActive)
            {
                string controlName = rename ? "CVRTrainerRenameInput" : "CVRTrainerGuidInput";
                HandleTextEntryEvent();
                GUI.SetNextControlName(controlName);
                if (rename)
                    _renameInput = GUI.TextField(inputRect, _renameInput, 64, _textFieldStyle);
                else
                    _guidInput = GUI.TextField(inputRect, _guidInput, 64, _textFieldStyle);

                if (_textFieldFocusPending)
                {
                    GUI.FocusControl(controlName);
                    _textFieldFocusPending = false;
                }
            }
            else
            {
                GUI.Label(inputRect, rename ? _renameInput : _guidInput, _textFieldStyle);
            }
        }

        void HandleTextEntryEvent()
        {
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.KeyDown)
                return;

            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter || evt.keyCode == KeyCode.Keypad5)
            {
                EndTextEntry();
                _selectedIndex = 1;
                ActivateSelected();
                evt.Use();
            }
            else if (evt.keyCode == KeyCode.Escape || evt.keyCode == KeyCode.Keypad0)
            {
                EndTextEntry();
                evt.Use();
            }
        }

        void DrawRow(float x, float y, string label, string value, bool selected)
        {
            var rowRect = new Rect(x, y, MenuWidth, RowHeight);
            GUI.DrawTexture(rowRect, selected ? _selectedTexture : _rowTexture);

            var labelRect = new Rect(x + 12f, y, MenuWidth - 24f, RowHeight);
            GUI.Label(labelRect, label, selected ? _selectedRowStyle : _rowStyle);

            if (string.IsNullOrEmpty(value))
                return;

            var valueRect = new Rect(x + 132f, y, MenuWidth - 144f, RowHeight);
            GUI.Label(valueRect, value, selected ? _selectedValueStyle : _valueStyle);
        }

        void HandleMouseRow(Rect rowRect, int index)
        {
            Event evt = Event.current;
            if (evt == null)
                return;

            Vector2 mousePos = evt.mousePosition;
            if (!rowRect.Contains(mousePos))
                return;

            if (_selectedIndex != index)
            {
                _selectedIndex = index;
                PlayNavigateSound();
            }

            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                if ((IsAddPage() || _page == MenuPage.RenameSpawnable) && index == 0)
                    BeginTextEntry();
                else
                    ActivateSelected();

                evt.Use();
            }
        }

        void HandleMouseWheel()
        {
            Event evt = Event.current;
            if (evt == null || evt.type != EventType.ScrollWheel || !_lastMenuRect.Contains(evt.mousePosition))
                return;

            if (Mathf.Abs(evt.delta.y) <= 0.01f)
                return;

            int direction = evt.delta.y > 0f ? 1 : -1;
            if (_page == MenuPage.VehicleSpawner && _selectedIndex == 1)
            {
                SetActiveIndex(GetActiveIndex() + direction);
                PlayNavigateSound();
            }
            else if (_page == MenuPage.VehicleSpawner && _selectedIndex == 0)
            {
                _spawnableFolder = (SpawnableFolder)Wrap((int)_spawnableFolder + direction, SpawnableFolderCount);
                PlayNavigateSound();
            }
            else
                MoveSelection(direction);

            evt.Use();
        }

        void MoveSelection(int direction)
        {
            int count = GetCurrentItems().Length;
            int next = (_selectedIndex + direction + count) % count;
            if (next != _selectedIndex)
            {
                _selectedIndex = next;
                PlayNavigateSound();
            }
        }

        void AdjustOption(int direction)
        {
            if (_page == MenuPage.VehicleSpawner)
            {
                if (_selectedIndex == 0)
                {
                    _spawnableFolder = (SpawnableFolder)Wrap((int)_spawnableFolder + direction, SpawnableFolderCount);
                    PlayNavigateSound();
                }
                else if (_selectedIndex == 1)
                {
                    SetActiveIndex(GetActiveIndex() + direction);
                    PlayNavigateSound();
                }
            }
        }

        void PlayNavigateSound()
        {
            if (_audioSource == null || _navigateClip == null)
                return;

            if (Time.unscaledTime - _lastSoundTime < 0.035f)
                return;

            _lastSoundTime = Time.unscaledTime;
            _audioSource.PlayOneShot(_navigateClip);
        }

        static AudioClip LoadEmbeddedWav(string resourceName)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            using (stream)
            using (var reader = new BinaryReader(stream))
            {
                if (new string(reader.ReadChars(4)) != "RIFF")
                    return null;

                reader.ReadInt32();
                if (new string(reader.ReadChars(4)) != "WAVE")
                    return null;

                int channels = 0;
                int sampleRate = 0;
                short bitsPerSample = 0;
                byte[] data = null;

                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string chunkId = new string(reader.ReadChars(4));
                    int chunkSize = reader.ReadInt32();
                    long nextChunk = reader.BaseStream.Position + chunkSize;

                    if (chunkId == "fmt ")
                    {
                        short audioFormat = reader.ReadInt16();
                        channels = reader.ReadInt16();
                        sampleRate = reader.ReadInt32();
                        reader.ReadInt32();
                        reader.ReadInt16();
                        bitsPerSample = reader.ReadInt16();

                        if (audioFormat != 1)
                            return null;
                    }
                    else if (chunkId == "data")
                    {
                        data = reader.ReadBytes(chunkSize);
                    }

                    reader.BaseStream.Position = nextChunk + (chunkSize % 2);
                }

                if (data == null || channels <= 0 || sampleRate <= 0)
                    return null;

                if (bitsPerSample != 8 && bitsPerSample != 16)
                    return null;

                int sampleCount = data.Length / (bitsPerSample / 8);
                float[] samples = new float[sampleCount];

                if (bitsPerSample == 16)
                {
                    for (int i = 0; i < sampleCount; i++)
                        samples[i] = System.BitConverter.ToInt16(data, i * 2) / 32768f;
                }
                else if (bitsPerSample == 8)
                {
                    for (int i = 0; i < sampleCount; i++)
                        samples[i] = (data[i] - 128) / 128f;
                }

                AudioClip clip = AudioClip.Create("CVRTrainerMenuNavigate", sampleCount / channels, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }
        }

        void ActivateSelected()
        {
            if (_page == MenuPage.Main)
            {
                ActivateMainItem();
                return;
            }

            if (_page == MenuPage.Player)
                ActivatePlayerItem();
            else if (_page == MenuPage.World)
                ActivateWorldItem();
            else if (_page == MenuPage.VehicleSpawner)
                ActivateVehicleItem();
            else if (_page == MenuPage.PropSpawner)
                ActivatePropItem();
            else if (IsAddPage())
                ActivateAddItem();
            else if (_page == MenuPage.RenameSpawnable)
                ActivateRenameItem();
        }

        void ActivateMainItem()
        {
            switch (_selectedIndex)
            {
                case 0:
                    OpenPage(MenuPage.Player);
                    break;
                case 1:
                    OpenPage(MenuPage.World);
                    break;
                case 2:
                    OpenPage(MenuPage.VehicleSpawner);
                    break;
                case 3:
                    OpenPage(MenuPage.PropSpawner);
                    break;
                case 4:
                    CloseMenu();
                    break;
            }
        }

        void ActivatePlayerItem()
        {
            switch (_selectedIndex)
            {
                case 0:
                    RootLogic.Instance.Respawn();
                    SetStatus("Respawn requested");
                    break;
                case 1:
                    SaveCurrentPosition();
                    break;
                case 2:
                    TeleportToSavedPosition();
                    break;
                case 3:
                    var controller = BetterBetterCharacterController.Instance;
                    if (controller == null)
                    {
                        SetStatus("Player controller not ready");
                        return;
                    }
                    controller.ToggleFlight();
                    SetStatus("Flight " + OnOff(controller.IsFlying()));
                    break;
                case 4:
                    controller = BetterBetterCharacterController.Instance;
                    if (controller == null)
                    {
                        SetStatus("Player controller not ready");
                        return;
                    }
                    controller.ToggleFlightNoClip();
                    SetStatus("Noclip " + OnOff(controller.IsFlyingNoClipEnabled()));
                    break;
                case 5:
                    controller = BetterBetterCharacterController.Instance;
                    if (controller == null)
                    {
                        SetStatus("Player controller not ready");
                        return;
                    }
                    EnableClipFlight(controller);
                    break;
                case 6:
                    Back();
                    break;
            }
        }

        void SaveCurrentPosition()
        {
            if (PlayerSetup.Instance == null)
            {
                SetStatus("Player setup not ready");
                return;
            }

            _savedPosition = PlayerSetup.Instance.GetPlayerPosition();
            _savedEulerRotation = PlayerSetup.Instance.GetPlayerRotation().eulerAngles;
            _hasSavedPosition = true;
            SetStatus("Position saved");
        }

        void TeleportToSavedPosition()
        {
            if (!_hasSavedPosition)
            {
                SetStatus("No saved position");
                return;
            }

            var controller = BetterBetterCharacterController.Instance;
            if (controller == null)
            {
                SetStatus("Player controller not ready");
                return;
            }

            controller.TeleportPlayerTo(_savedPosition, _savedEulerRotation, false, true, false);
            SetStatus("Teleported to saved position");
        }

        void ActivateWorldItem()
        {
            switch (_selectedIndex)
            {
                case 0:
                    Content.LoadIntoWorld(Instances.CurrentWorldId, CVRObjectLoader.WorldLoadIntent.Join);
                    SetStatus("Reloading world");
                    break;
                case 1:
                    ViewManager.Instance.GoHome();
                    SetStatus("Going home");
                    break;
                case 2:
                    GUIUtility.systemCopyBuffer = Instances.CurrentWorldId;
                    SetStatus("Copied world ID");
                    break;
                case 3:
                    GUIUtility.systemCopyBuffer = Instances.CurrentInstanceId;
                    SetStatus("Copied instance ID");
                    break;
                case 4:
                    if (PlayerSetup.Instance == null || string.IsNullOrEmpty(Instances.CurrentInstanceId))
                    {
                        SetStatus("No instance to portal");
                        return;
                    }
                    PlayerSetup.Instance.DropPortal(Instances.CurrentInstanceId);
                    CloseMenuForGameControls("Portal dropped");
                    break;
                case 5:
                    Back();
                    break;
            }
        }

        void EnableClipFlight(BetterBetterCharacterController controller)
        {
            if (controller.IsFlyingNoClipEnabled())
                controller.ToggleFlightNoClip();

            if (!controller.IsFlying())
                controller.ToggleFlight();

            SetStatus(controller.IsFlying() && !controller.IsFlyingNoClipEnabled()
                ? "Clip Flight ON"
                : "Clip Flight unavailable");
        }

        void ActivateVehicleItem()
        {
            switch (_selectedIndex)
            {
                case 0:
                    AdjustOption(1);
                    break;
                case 1:
                    AdjustOption(1);
                    break;
                case 2:
                    OpenAddForCurrentFolder();
                    break;
                case 3:
                    AddGuidFromClipboard(IsVehicleFolder());
                    break;
                case 4:
                    ResolveActiveSelected();
                    break;
                case 5:
                    SelectToSpawnSelected();
                    break;
                case 6:
                    SpawnActiveSelected();
                    break;
                case 7:
                    DeleteLastSpawned(_spawnableFolder != SpawnableFolder.Props);
                    break;
                case 8:
                    AddActiveToFavorites();
                    break;
                case 9:
                    MoveActiveEntry(-1);
                    break;
                case 10:
                    MoveActiveEntry(1);
                    break;
                case 11:
                    OpenRenameForActive();
                    break;
                case 12:
                    RemoveActiveEntry();
                    break;
                case 13:
                    Back();
                    break;
            }
        }

        void ActivatePropItem()
        {
            switch (_selectedIndex)
            {
                case 0:
                    CVRSyncHelper.DeleteMyProps();
                    SetStatus("Delete my props requested");
                    break;
                case 1:
                    CVRSyncHelper.DeleteAllProps();
                    SetStatus("Delete all props requested");
                    break;
                case 2:
                    if (PlayerSetup.Instance == null)
                    {
                        SetStatus("Player setup not ready");
                        return;
                    }
                    PlayerSetup.Instance.EnterPropDeleteMode();
                    CloseMenuForGameControls("Prop delete mode");
                    break;
                case 3:
                    if (PlayerSetup.Instance != null)
                        PlayerSetup.Instance.ClearPropToSpawn();
                    SetStatus("Prop mode cleared");
                    break;
                case 4:
                    Back();
                    break;
            }
        }

        void OpenAddForCurrentFolder()
        {
            if (_spawnableFolder == SpawnableFolder.Props)
                OpenPage(MenuPage.AddProp);
            else
                OpenPage(MenuPage.AddVehicle);
        }

        bool IsVehicleFolder()
        {
            return _spawnableFolder != SpawnableFolder.Props;
        }

        void SelectToSpawnSelected()
        {
            SavedContentEntry entry = GetActiveEntry();
            if (entry == null)
            {
                SetStatus("No saved spawnable");
                return;
            }

            if (PlayerSetup.Instance == null)
            {
                SetStatus("Player setup not ready");
                return;
            }

            PlayerSetup.Instance.SelectPropToSpawn(entry.Guid, string.Empty, entry.DisplayName);
            Settings.AddRecent(entry);
            CloseMenuForGameControls("Selected for spawning");
        }

        void CloseMenuForGameControls(string status)
        {
            SetStatus(status);
            CloseMenu();
        }

        void SpawnActiveSelected()
        {
            SavedContentEntry entry = GetActiveEntry();

            if (entry == null)
            {
                SetStatus("Add a spawnable GUID first");
                return;
            }

            if (!entry.IsPermitted)
            {
                SetStatus("This spawnable is not permitted");
                return;
            }

            if (PlayerSetup.Instance == null)
            {
                SetStatus("Player setup not ready");
                return;
            }

            HashSet<string> existingInstances = GetOwnPropInstances(entry.Guid);
            PlayerSetup.Instance.DropProp(entry.Guid);
            Settings.AddRecent(entry);
            SetStatus("Spawn requested");
            StartCoroutine(CaptureLastSpawnedInstance(entry.Guid, _spawnableFolder != SpawnableFolder.Props, existingInstances));
        }

        IEnumerator CaptureLastSpawnedInstance(string guid, bool vehicle, HashSet<string> existingInstances)
        {
            float timeout = Time.unscaledTime + 8f;
            while (Time.unscaledTime < timeout)
            {
                string instanceId = FindNewestOwnPropInstance(guid, existingInstances);
                if (!string.IsNullOrEmpty(instanceId))
                {
                    if (vehicle)
                        _lastVehicleInstanceId = instanceId;
                    else
                        _lastPropInstanceId = instanceId;
                    yield break;
                }

                yield return null;
            }
        }

        void DeleteLastSpawned(bool vehicle)
        {
            string instanceId = vehicle ? _lastVehicleInstanceId : _lastPropInstanceId;
            if (string.IsNullOrEmpty(instanceId))
            {
                SetStatus(vehicle ? "No spawned vehicle tracked" : "No spawned prop tracked");
                return;
            }

            CVRSyncHelper.DeleteMyPropByInstanceIdOverNetwork(instanceId);
            if (vehicle)
                _lastVehicleInstanceId = string.Empty;
            else
                _lastPropInstanceId = string.Empty;

            SetStatus(vehicle ? "Vehicle delete requested" : "Prop delete requested");
        }

        void ResolveActiveSelected()
        {
            SavedContentEntry entry = GetActiveEntry();
            if (entry == null)
            {
                SetStatus("No saved spawnable");
                return;
            }

            SetStatus("Resolving " + Settings.ShortGuid(entry.Guid));
            StartCoroutine(ResolveEntry(entry, _spawnableFolder != SpawnableFolder.Props));
        }

        void AddActiveToFavorites()
        {
            SavedContentEntry entry = GetActiveEntry();
            if (entry == null)
            {
                SetStatus("No saved spawnable");
                return;
            }

            SetStatus(Settings.AddFavorite(entry) ? "Added favorite" : "Already favorite");
        }

        void MoveActiveEntry(int direction)
        {
            List<SavedContentEntry> list = GetActiveList();
            int index = GetActiveIndex();
            int target = index + direction;

            if (list.Count == 0)
            {
                SetStatus("No entry to move");
                return;
            }

            if (target < 0 || target >= list.Count)
            {
                SetStatus(direction < 0 ? "Already at top" : "Already at bottom");
                return;
            }

            switch (_spawnableFolder)
            {
                case SpawnableFolder.Vehicles:
                    Settings.MoveVehicle(index, direction);
                    break;
                case SpawnableFolder.Props:
                    Settings.MoveProp(index, direction);
                    break;
                case SpawnableFolder.Favorites:
                    Settings.MoveFavorite(index, direction);
                    break;
                default:
                    SetStatus("Recent order is automatic");
                    return;
            }

            SetActiveIndex(target);
            SetStatus(direction < 0 ? "Moved up" : "Moved down");
        }

        void OpenRenameForActive()
        {
            SavedContentEntry entry = GetActiveEntry();
            if (entry == null)
            {
                SetStatus("No saved spawnable");
                return;
            }

            _renameInput = entry.LocalLabel;
            OpenPage(MenuPage.RenameSpawnable);
        }

        void ActivateRenameItem()
        {
            SavedContentEntry entry = GetActiveEntry();
            if (entry == null)
            {
                Back();
                return;
            }

            switch (_selectedIndex)
            {
                case 1:
                    entry.LocalLabel = _renameInput.Trim();
                    SaveActiveEntry(entry);
                    Back();
                    SetStatus("Label saved");
                    break;
                case 2:
                    entry.LocalLabel = string.Empty;
                    SaveActiveEntry(entry);
                    Back();
                    SetStatus("Label cleared");
                    break;
                case 3:
                    Back();
                    break;
            }
        }

        void RemoveActiveEntry()
        {
            List<SavedContentEntry> list = GetActiveList();
            int index = GetActiveIndex();
            if (list.Count == 0)
            {
                SetStatus("No entry to remove");
                return;
            }

            switch (_spawnableFolder)
            {
                case SpawnableFolder.Vehicles:
                    Settings.RemoveVehicle(index);
                    break;
                case SpawnableFolder.Props:
                    Settings.RemoveProp(index);
                    break;
                case SpawnableFolder.Favorites:
                    Settings.RemoveFavorite(index);
                    break;
                case SpawnableFolder.Recent:
                    Settings.RemoveRecent(index);
                    break;
            }

            ClampActiveIndex(index);
            SetStatus("Removed entry");
        }

        SavedContentEntry GetActiveEntry()
        {
            return GetSelectedEntry(GetActiveList(), GetActiveIndex());
        }

        List<SavedContentEntry> GetActiveList()
        {
            switch (_spawnableFolder)
            {
                case SpawnableFolder.Props:
                    return Settings.Props;
                case SpawnableFolder.Favorites:
                    return Settings.Favorites;
                case SpawnableFolder.Recent:
                    return Settings.Recent;
                default:
                    return Settings.Vehicles;
            }
        }

        int GetActiveIndex()
        {
            switch (_spawnableFolder)
            {
                case SpawnableFolder.Props:
                    return _propIndex;
                case SpawnableFolder.Favorites:
                    return _favoriteIndex;
                case SpawnableFolder.Recent:
                    return _recentIndex;
                default:
                    return _vehicleIndex;
            }
        }

        void SetActiveIndex(int value)
        {
            int count = Mathf.Max(1, GetActiveList().Count);
            int index = Wrap(value, count);
            switch (_spawnableFolder)
            {
                case SpawnableFolder.Props:
                    _propIndex = index;
                    break;
                case SpawnableFolder.Favorites:
                    _favoriteIndex = index;
                    break;
                case SpawnableFolder.Recent:
                    _recentIndex = index;
                    break;
                default:
                    _vehicleIndex = index;
                    break;
            }
        }

        void ClampActiveIndex(int value)
        {
            int count = GetActiveList().Count;
            int index = count == 0 ? 0 : Mathf.Clamp(value, 0, count - 1);
            switch (_spawnableFolder)
            {
                case SpawnableFolder.Props:
                    _propIndex = index;
                    break;
                case SpawnableFolder.Favorites:
                    _favoriteIndex = index;
                    break;
                case SpawnableFolder.Recent:
                    _recentIndex = index;
                    break;
                default:
                    _vehicleIndex = index;
                    break;
            }
        }

        void SaveActiveEntry(SavedContentEntry entry)
        {
            switch (_spawnableFolder)
            {
                case SpawnableFolder.Props:
                    Settings.UpdateProp(entry);
                    break;
                case SpawnableFolder.Favorites:
                    Settings.UpdateFavorite(entry);
                    break;
                case SpawnableFolder.Recent:
                    Settings.UpdateRecent(entry);
                    break;
                default:
                    Settings.UpdateVehicle(entry);
                    break;
            }
        }

        static HashSet<string> GetOwnPropInstances(string guid)
        {
            var instances = new HashSet<string>();
            if (MetaPort.Instance == null)
                return instances;

            for (int i = 0; i < CVRSyncHelper.Props.Count; i++)
            {
                CVRSyncHelper.PropData prop = CVRSyncHelper.Props[i];
                if (prop == null || prop.ObjectId != guid || prop.SpawnedBy != MetaPort.Instance.ownerId)
                    continue;

                if (!string.IsNullOrEmpty(prop.InstanceId))
                    instances.Add(prop.InstanceId);
            }

            return instances;
        }

        static string FindNewestOwnPropInstance(string guid, HashSet<string> existingInstances)
        {
            if (MetaPort.Instance == null)
                return string.Empty;

            for (int i = CVRSyncHelper.Props.Count - 1; i >= 0; i--)
            {
                CVRSyncHelper.PropData prop = CVRSyncHelper.Props[i];
                if (prop == null || prop.ObjectId != guid || prop.SpawnedBy != MetaPort.Instance.ownerId)
                    continue;

                if (!string.IsNullOrEmpty(prop.InstanceId) && !existingInstances.Contains(prop.InstanceId))
                    return prop.InstanceId;
            }

            return string.Empty;
        }

        void ActivateAddItem()
        {
            switch (_selectedIndex)
            {
                case 1:
                    AddGuid(_page == MenuPage.AddVehicle, _guidInput);
                    break;
                case 2:
                    _guidInput = GUIUtility.systemCopyBuffer;
                    AddGuid(_page == MenuPage.AddVehicle, _guidInput);
                    break;
                case 3:
                    Back();
                    break;
            }
        }

        void AddGuidFromClipboard(bool vehicle)
        {
            AddGuid(vehicle, GUIUtility.systemCopyBuffer);
        }

        void AddGuid(bool vehicle, string rawGuid)
        {
            bool added = vehicle ? Settings.AddVehicle(rawGuid) : Settings.AddProp(rawGuid);
            if (!added)
            {
                SetStatus("Invalid or duplicate GUID");
                return;
            }

            List<SavedContentEntry> list = vehicle ? Settings.Vehicles : Settings.Props;
            int index = list.Count - 1;
            if (vehicle)
                _vehicleIndex = index;
            else
                _propIndex = index;

            _guidInput = string.Empty;
            SetStatus("Saved GUID, resolving details");
            StartCoroutine(ResolveEntry(list[index], vehicle));
        }

        IEnumerator ResolveSavedEntries()
        {
            for (int i = 0; i < Settings.Vehicles.Count; i++)
                yield return ResolveEntry(Settings.Vehicles[i], true);

            for (int i = 0; i < Settings.Props.Count; i++)
                yield return ResolveEntry(Settings.Props[i], false);

            for (int i = 0; i < Settings.Favorites.Count; i++)
                yield return ResolveEntry(Settings.Favorites[i], true);

            for (int i = 0; i < Settings.Recent.Count; i++)
                yield return ResolveEntry(Settings.Recent[i], true);
        }

        IEnumerator ResolveEntry(SavedContentEntry entry, bool vehicle)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Guid))
                yield break;

            ContentSpawnableResponse cached;
            if (ContentSpawnableResponse.Cache.TryGetValue(entry.Guid, out cached))
            {
                ApplyResolvedDetails(entry, cached, vehicle);
                yield break;
            }

            entry.Status = "Resolving";
            SaveResolvedEntry(entry, vehicle);

            var viewManager = ViewManager.Instance;
            if (viewManager == null)
            {
                entry.Status = "UI not ready";
                SaveResolvedEntry(entry, vehicle);
                yield break;
            }

            var task = viewManager.GetPropDetailsTask(entry.Guid);
            float timeout = Time.unscaledTime + 12f;
            while (!task.IsCompleted && Time.unscaledTime < timeout)
                yield return null;

            if (ContentSpawnableResponse.Cache.TryGetValue(entry.Guid, out cached))
            {
                ApplyResolvedDetails(entry, cached, vehicle);
                yield break;
            }

            entry.Name = string.IsNullOrEmpty(entry.Name) || entry.Name == "Resolving..." ? Settings.ShortGuid(entry.Guid) : entry.Name;
            entry.Status = task.IsFaulted ? "Resolve failed" : "No details";
            SaveResolvedEntry(entry, vehicle);
        }

        void ApplyResolvedDetails(SavedContentEntry entry, ContentSpawnableResponse details, bool vehicle)
        {
            entry.Name = string.IsNullOrEmpty(details.Name) ? Settings.ShortGuid(entry.Guid) : details.Name;
            entry.Author = details.Author != null ? details.Author.Name : string.Empty;
            entry.IsPermitted = details.Permitted;
            entry.IsPublic = details.Public;
            entry.Status = details.Permitted ? "Ready" : "Not permitted";
            SaveResolvedEntry(entry, vehicle);
        }

        void SaveResolvedEntry(SavedContentEntry entry, bool vehicle)
        {
            if (Settings.Vehicles.Contains(entry))
                Settings.UpdateVehicle(entry);
            else if (Settings.Props.Contains(entry))
                Settings.UpdateProp(entry);
            else if (Settings.Favorites.Contains(entry))
                Settings.UpdateFavorite(entry);
            else if (Settings.Recent.Contains(entry))
                Settings.UpdateRecent(entry);
        }

        void Back()
        {
            if (_page == MenuPage.Main)
            {
                CloseMenu();
                return;
            }

            if (_page == MenuPage.RenameSpawnable)
                OpenPage(MenuPage.VehicleSpawner);
            else if (_page == MenuPage.AddVehicle)
                OpenPage(MenuPage.VehicleSpawner);
            else if (_page == MenuPage.AddProp)
                OpenPage(MenuPage.PropSpawner);
            else
                OpenPage(MenuPage.Main);
        }

        void OpenPage(MenuPage page)
        {
            _page = page;
            _selectedIndex = 0;
            EndTextEntry();
            if (IsAddPage())
                _guidInput = string.Empty;
            SetStatus("Ready");
        }

        string[] GetCurrentItems()
        {
            switch (_page)
            {
                case MenuPage.Player:
                    return PlayerItems;
                case MenuPage.World:
                    return WorldItems;
                case MenuPage.VehicleSpawner:
                    return VehicleItems;
                case MenuPage.PropSpawner:
                    return PropItems;
                case MenuPage.AddVehicle:
                case MenuPage.AddProp:
                    return AddItems;
                case MenuPage.RenameSpawnable:
                    return RenameItems;
                default:
                    return MainItems;
            }
        }

        string GetPageTitle()
        {
            switch (_page)
            {
                case MenuPage.Player:
                    return "PLAYER";
                case MenuPage.World:
                    return "WORLD";
                case MenuPage.VehicleSpawner:
                    return "VEHICLE SPAWNER";
                case MenuPage.PropSpawner:
                    return "PROPS";
                case MenuPage.AddVehicle:
                case MenuPage.AddProp:
                    return "ADD SPAWNABLE GUID";
                case MenuPage.RenameSpawnable:
                    return "RENAME LABEL";
                default:
                    return "MAIN MENU";
            }
        }

        string GetItemValue(int index)
        {
            if (_page == MenuPage.Main)
                return ">";

            if (_page == MenuPage.Player)
            {
                var controller = BetterBetterCharacterController.Instance;
                switch (index)
                {
                    case 0:
                        return string.Empty;
                    case 1:
                        return _hasSavedPosition ? "Saved" : "Empty";
                    case 2:
                        return _hasSavedPosition ? "Ready" : "No save";
                    case 3:
                        return controller == null ? "N/A" : OnOff(controller.IsFlying());
                    case 4:
                        return controller == null ? "N/A" : OnOff(controller.IsFlyingNoClipEnabled());
                    case 5:
                        return controller == null ? "N/A" : OnOff(controller.IsFlying() && !controller.IsFlyingNoClipEnabled());
                }
            }
            else if (_page == MenuPage.World)
            {
                switch (index)
                {
                    case 2:
                        return Settings.ShortGuid(Instances.CurrentWorldId);
                    case 3:
                        return Settings.ShortGuid(Instances.CurrentInstanceId);
                    case 4:
                        return string.IsNullOrEmpty(Instances.CurrentInstanceId) ? "No instance" : "Current";
                }
            }
            else if (_page == MenuPage.VehicleSpawner)
            {
                SavedContentEntry entry = GetActiveEntry();
                switch (index)
                {
                    case 0:
                        return "< " + _spawnableFolder + " >";
                    case 1:
                        return entry == null ? "< none >" : "< " + entry.DisplayName + " >";
                    case 2:
                    case 3:
                        return (_spawnableFolder == SpawnableFolder.Favorites || _spawnableFolder == SpawnableFolder.Recent) ? "Use base folder" : string.Empty;
                    case 4:
                    case 5:
                    case 6:
                    case 7:
                    case 8:
                    case 9:
                    case 10:
                    case 11:
                    case 12:
                        return entry == null ? "No GUID" : string.Empty;
                }
            }
            else if (IsAddPage())
            {
                switch (index)
                {
                    case 1:
                        return string.IsNullOrEmpty(Settings.NormalizeGuid(_guidInput)) ? "Needs GUID" : "Ready";
                    case 2:
                        return "Clipboard";
                }
            }

            return string.Empty;
        }

        string GetFooterText()
        {
            if (Time.unscaledTime < _statusUntil)
                return _status;

            SavedContentEntry entry = null;
            if (_page == MenuPage.VehicleSpawner)
                entry = GetActiveEntry();

            if (entry != null)
            {
                string author = string.IsNullOrEmpty(entry.Author) ? "" : " by " + entry.Author;
                return entry.Status + author + " | " + Settings.ShortGuid(entry.Guid);
            }

            return "F4 open/close   8/2 or arrows move   4/6 change   5/Enter select   0/O back";
        }

        void SetStatus(string message)
        {
            _status = message;
            _statusUntil = Time.unscaledTime + 2.2f;
        }

        void BeginTextEntry()
        {
            _textFieldActive = true;
            _textFieldFocusPending = true;
        }

        void EndTextEntry()
        {
            _textFieldActive = false;
            _textFieldFocusPending = false;
            GUI.FocusControl(null);
        }

        void SetMenuOpen(bool open)
        {
            _isOpen = open;
            SetGameInputLocked(open);

            ResetTransientMenuState();

            if (open)
                SetStatus("Ready");
        }

        void ResetTransientMenuState()
        {
            _page = MenuPage.Main;
            _selectedIndex = 0;
            _guidInput = string.Empty;
            _renameInput = string.Empty;
            _lastMenuRect = default(Rect);
            EndTextEntry();
        }

        void CloseMenu()
        {
            if (!_isOpen)
                return;

            _isOpen = false;
            SetGameInputLocked(false);
            ResetTransientMenuState();
        }

        void SetGameInputLocked(bool locked)
        {
            if (_gameInputLocked == locked)
                return;

            _gameInputLocked = locked;
            CVRInputManager._moduleKeyboard.InputLock.SetLock(InputLockId, locked);
            CVRInputManager._moduleMouse.InputLock.SetLock(InputLockId, locked);
            CVRInputManager._moduleGamepad.InputLock.SetLock(InputLockId, locked);

            if (locked)
                ClearGameInputState();
        }

        static void ClearGameInputState()
        {
            var input = CVRInputManager.Instance;
            if (input == null)
                return;

            input.movementVector = Vector3.zero;
            input.lookVector = Vector2.zero;
            input.rawLookVector = Vector2.zero;
            input.jump = false;
            input.sprint = false;
            input.crouchToggle = false;
            input.proneToggle = false;
            input.floatDirection = 0f;
            input.rollDirection = 0f;
            input.mainMenuButton = false;
            input.quickMenuButton = false;
            input.interactLeftDown = false;
            input.interactLeftUp = false;
            input.interactRightDown = false;
            input.interactRightUp = false;
            input.drop = false;
            input.toggleFlight = false;
            input.reload = false;
            input.switchMode = false;
            input.unlockMouse = false;
            input.accelerate = 0f;
            input.brake = 0f;
            input.steering = 0f;
            input.handBrake = 0f;
            input.boost = 0f;
        }

        void CreateStyles()
        {
            _headerTexture = MakeTexture(new Color(0.02f, 0.02f, 0.02f, 0.96f));
            _subHeaderTexture = MakeTexture(new Color(0.10f, 0.10f, 0.10f, 0.94f));
            _bodyTexture = MakeTexture(new Color(0.01f, 0.01f, 0.01f, 0.82f));
            _rowTexture = MakeTexture(new Color(0f, 0f, 0f, 0.18f));
            _selectedTexture = MakeTexture(new Color(0.95f, 0.95f, 0.95f, 0.95f));
            _accentTexture = MakeTexture(new Color(0.94f, 0.74f, 0.16f, 1f));
            _footerTexture = MakeTexture(new Color(0.04f, 0.04f, 0.04f, 0.94f));

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 27,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            _subTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.74f, 0.16f, 1f) }
            };

            _rowStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 14,
                normal = { textColor = new Color(0.92f, 0.92f, 0.92f, 1f) }
            };

            _selectedRowStyle = new GUIStyle(_rowStyle)
            {
                normal = { textColor = Color.black }
            };

            _valueStyle = new GUIStyle(_rowStyle)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.78f, 0.78f, 0.78f, 1f) }
            };

            _selectedValueStyle = new GUIStyle(_valueStyle)
            {
                normal = { textColor = Color.black }
            };

            _footerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = new Color(1f, 1f, 1f, 0.58f) }
            };

            _textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                normal = { textColor = Color.white }
            };
        }

        bool IsAddPage()
        {
            return _page == MenuPage.AddVehicle || _page == MenuPage.AddProp;
        }

        static SavedContentEntry GetSelectedEntry(List<SavedContentEntry> list, int index)
        {
            if (list == null || list.Count == 0)
                return null;

            return list[Mathf.Clamp(index, 0, list.Count - 1)];
        }

        static bool Pressed(params KeyCode[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (Input.GetKeyDown(keys[i]))
                    return true;
            }

            return false;
        }

        static int Wrap(int value, int count)
        {
            if (count <= 0)
                return 0;

            return (value + count) % count;
        }

        static string OnOff(bool value)
        {
            return value ? "ON" : "OFF";
        }

        static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        static void DestroyTexture(Texture2D texture)
        {
            if (texture != null)
                Object.Destroy(texture);
        }
    }
}
