using System;
using UnityEditor.Callbacks;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEditor.TestTools.TestRunner.GUI;
using UnityEngine;

namespace UnityEditor.TestTools.TestRunner
{
    [Serializable]
    internal class TestRunnerWindow : EditorWindow, IHasCustomMenu
    {
        internal static class Styles
        {
            public static GUIStyle info;
            public static GUIStyle testList;

            static Styles()
            {
                info = new GUIStyle(EditorStyles.wordWrappedLabel);
                info.wordWrap = false;
                info.stretchHeight = true;
                info.margin.right = 15;

                testList = new GUIStyle("CN Box");
                testList.margin.top = 0;
                testList.padding.left = 3;
            }
        }

        private readonly GUIContent m_GUIHorizontalSplit = EditorGUIUtility.TrTextContent("Horizontal layout");
        private readonly GUIContent m_GUIVerticalSplit = EditorGUIUtility.TrTextContent("Vertical layout");
        private readonly GUIContent m_GUIEnableaPlaymodeTestsRunner = EditorGUIUtility.TrTextContent("Enable playmode tests for all assemblies");
        private readonly GUIContent m_GUIDisablePlaymodeTestsRunner = EditorGUIUtility.TrTextContent("Disable playmode tests for all assemblies");
        private readonly GUIContent m_GUIRunPlayModeTestAsEditModeTests = EditorGUIUtility.TrTextContent("Run playmode tests as editmode tests");

        internal static TestRunnerWindow s_Instance;
        private bool m_IsBuilding;
        [NonSerialized]
        private bool m_Enabled;
        public TestFilterSettings filterSettings;

        private readonly SplitterState m_Spl = new SplitterState(new float[] { 75, 25 }, new[] { 32, 32 }, null);

        private TestRunnerWindowSettings m_Settings;

        private enum TestRunnerMenuLabels
        {
            PlayMode = 0,
            EditMode = 1
        }
        [SerializeField]
        private int m_TestTypeToolbarIndex = (int)TestRunnerMenuLabels.EditMode;
        [SerializeField]
        private PlayModeTestListGUI m_PlayModeTestListGUI;
        [SerializeField]
        private EditModeTestListGUI m_EditModeTestListGUI;

        internal TestListGUI m_SelectedTestTypes;

        private ITestRunnerApi m_testRunnerApi;

        private WindowResultUpdater m_WindowResultUpdater;

        [MenuItem("Window/General/Test Runner", false, 201, false)]
        public static void ShowPlaymodeTestsRunnerWindowCodeBased()
        {
            s_Instance = GetWindow<TestRunnerWindow>("Test Runner");
            s_Instance.Show();
        }

        static TestRunnerWindow()
        {
            InitBackgroundRunners();
        }

        private static void InitBackgroundRunners()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [DidReloadScripts]
        private static void CompilationCallback()
        {
            UpdateWindow();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (s_Instance && state == PlayModeStateChange.EnteredEditMode && s_Instance.m_SelectedTestTypes.HasTreeData())
            {
                //repaint message details after exit playmode
                s_Instance.m_SelectedTestTypes.TestSelectionCallback(s_Instance.m_SelectedTestTypes.m_TestListState.selectedIDs.ToArray());
                s_Instance.Repaint();
            }
        }

        public void OnDestroy()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            if (m_testRunnerApi != null)
            {
                m_testRunnerApi.UnregisterCallbacks(m_WindowResultUpdater);
            }
        }

        private void OnEnable()
        {
            s_Instance = this;
            SelectTestListGUI(m_TestTypeToolbarIndex);

            m_testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            m_WindowResultUpdater = new WindowResultUpdater();
            m_testRunnerApi.RegisterCallbacks(m_WindowResultUpdater);
        }

        private void Enable()
        {
            m_Settings = new TestRunnerWindowSettings("UnityEditor.PlaymodeTestsRunnerWindow");
            filterSettings = new TestFilterSettings("UnityTest.IntegrationTestsRunnerWindow");

            if (m_SelectedTestTypes == null)
            {
                SelectTestListGUI(m_TestTypeToolbarIndex);
            }

            StartRetrieveTestList();
            m_SelectedTestTypes.Reload();
            m_Enabled = true;
        }

        private void SelectTestListGUI(int testTypeToolbarIndex)
        {
            if (testTypeToolbarIndex == (int)TestRunnerMenuLabels.PlayMode)
            {
                if (m_PlayModeTestListGUI == null)
                {
                    m_PlayModeTestListGUI = new PlayModeTestListGUI();
                }
                m_SelectedTestTypes = m_PlayModeTestListGUI;
            }
            else if (testTypeToolbarIndex == (int)TestRunnerMenuLabels.EditMode)
            {
                if (m_EditModeTestListGUI == null)
                {
                    m_EditModeTestListGUI = new EditModeTestListGUI();
                }
                m_SelectedTestTypes = m_EditModeTestListGUI;
            }
        }

        private void StartRetrieveTestList()
        {
            if (!m_SelectedTestTypes.HasTreeData())
            {
                m_testRunnerApi.RetrieveTestList(new ExecutionSettings() { filters = new []{new Filter() { testMode = m_SelectedTestTypes.TestMode } }}, (rootTest) =>
                {
                    m_SelectedTestTypes.Init(this, rootTest);
                    m_SelectedTestTypes.Reload();
                });
            }
        }

        public void OnGUI()
        {
            if (!m_Enabled)
            {
                Enable();
            }

            if (BuildPipeline.isBuildingPlayer)
            {
                m_IsBuilding = true;
            }
            else if (m_IsBuilding)
            {
                m_IsBuilding = false;
                Repaint();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            var selectedIndex = m_TestTypeToolbarIndex;
            m_TestTypeToolbarIndex = GUILayout.Toolbar(m_TestTypeToolbarIndex, Enum.GetNames(typeof(TestRunnerMenuLabels)), "LargeButton", UnityEngine.GUI.ToolbarButtonSize.FitToContents);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (selectedIndex != m_TestTypeToolbarIndex)
            {
                SelectTestListGUI(m_TestTypeToolbarIndex);
                StartRetrieveTestList();
            }

            EditorGUILayout.BeginVertical();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                m_SelectedTestTypes.PrintHeadPanel();
            }
            EditorGUILayout.EndVertical();

            if (m_Settings.verticalSplit)
                SplitterGUILayout.BeginVerticalSplit(m_Spl);
            else
                SplitterGUILayout.BeginHorizontalSplit(m_Spl);

            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginVertical(Styles.testList);
            m_SelectedTestTypes.RenderTestList();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();

            m_SelectedTestTypes.RenderDetails();

            if (m_Settings.verticalSplit)
                SplitterGUILayout.EndVerticalSplit();
            else
                SplitterGUILayout.EndHorizontalSplit();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(m_GUIVerticalSplit, m_Settings.verticalSplit, m_Settings.ToggleVerticalSplit);
            menu.AddItem(m_GUIHorizontalSplit, !m_Settings.verticalSplit, m_Settings.ToggleVerticalSplit);

            menu.AddSeparator(null);

            var playModeTestRunnerEnabled = PlayerSettings.playModeTestRunnerEnabled;
            var currentActive = playModeTestRunnerEnabled ? m_GUIDisablePlaymodeTestsRunner : m_GUIEnableaPlaymodeTestsRunner;

            if (EditorPrefs.GetBool("InternalMode", false))
            {
                menu.AddItem(m_GUIRunPlayModeTestAsEditModeTests, PlayerSettings.runPlayModeTestAsEditModeTest, () =>
                {
                    PlayerSettings.runPlayModeTestAsEditModeTest = !PlayerSettings.runPlayModeTestAsEditModeTest;
                });
            }

            menu.AddItem(currentActive, false, () =>
            {
                PlayerSettings.playModeTestRunnerEnabled = !playModeTestRunnerEnabled;
                EditorUtility.DisplayDialog(currentActive.text, "You need to restart the editor now", "Ok");
            });
        }

        public void RebuildUIFilter()
        {
            if (m_SelectedTestTypes != null && m_SelectedTestTypes.HasTreeData())
            {
                m_SelectedTestTypes.RebuildUIFilter();
            }
        }

        public static void UpdateWindow()
        {
            if (s_Instance != null && s_Instance.m_SelectedTestTypes != null)
            {
                s_Instance.m_SelectedTestTypes.Repaint();
                s_Instance.Repaint();
            }
        }
    }
}