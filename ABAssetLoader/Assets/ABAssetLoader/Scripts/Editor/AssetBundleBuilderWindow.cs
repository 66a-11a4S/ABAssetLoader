using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ABAssetLoader.Editor
{
    public class AssetBundleBuilderWindow : EditorWindow
    {
        [MenuItem("Window/ABAssetLoader/AssetBundleBuilder の起動")]
        public static void CreateWindow()
        {
            GetWindow<AssetBundleBuilderWindow>();
        }

        private enum TargetOS
        {
            Windows,
            OSX,
            Android,
            iOS
        }

        private string _packageName;
        private string _outputFolder;
        private bool _exportAsInitialPackage;
        private Dictionary<TargetOS, bool> _selectedTargetOS;
        private readonly Dictionary<string, bool> _selectedBundles = new();

        private void OnEnable()
        {
            _selectedTargetOS = Enum.GetValues(typeof(TargetOS)).Cast<TargetOS>()
                .ToDictionary(targetOS => targetOS, _ => false);
        }

        private void OnGUI()
        {
            if (GUILayout.Button("バンドル一覧の読み込み"))
                LoadBundle();

            ShowBundles();

            EditorGUILayout.Space();

            _exportAsInitialPackage = EditorGUILayout.Toggle("初期パッケージとして出力", _exportAsInitialPackage);
            using (new EditorGUI.DisabledScope(disabled: _exportAsInitialPackage))
            {
                _packageName = EditorGUILayout.TextField("パッケージ名", _packageName);
                if (string.IsNullOrEmpty(_packageName) && !_exportAsInitialPackage)
                    EditorGUILayout.HelpBox("パッケージ名を入力してください", MessageType.Warning);

                if (GUILayout.Button("出力先を選択"))
                    _outputFolder = EditorUtility.OpenFolderPanel("出力先を選択", folder: "", defaultName: "");
                if (string.IsNullOrEmpty(_outputFolder) && !_exportAsInitialPackage)
                    EditorGUILayout.HelpBox("出力先を選択してください", MessageType.Warning);
            }

            EditorGUILayout.LabelField($"出力先 - {_outputFolder}");

            EditorGUILayout.Space();

            ShowBuildTarget();

            if (GUILayout.Button("パッケージを作成"))
            {
                var packageName = _exportAsInitialPackage ? AssetLoaderSetting.BundleBasePath : _packageName;
                var outputFolder = _exportAsInitialPackage ? Application.streamingAssetsPath : _outputFolder;
                var contentNames = _selectedBundles.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToHashSet();
                var selectedTargetOS = _selectedTargetOS.First(kvp => kvp.Value).Key;
                BuildBundle(packageName, outputFolder, contentNames, selectedTargetOS);
            }
        }

        private void LoadBundle()
        {
            _selectedBundles.Clear();
            foreach (var bundleName in AssetDatabase.GetAllAssetBundleNames())
            {
                _selectedBundles[bundleName] = false;
            }
        }

        private void ShowBundles()
        {
            EditorGUILayout.LabelField("バンドル一覧:");

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var bundleName in _selectedBundles.Keys.ToArray())
                {
                    _selectedBundles[bundleName] = GUILayout.Toggle(_selectedBundles[bundleName], bundleName);
                }
            }

            if (!_selectedBundles.Any(kvp => kvp.Value))
                EditorGUILayout.HelpBox("パッケージに含めるバンドルが選択されていません", MessageType.Warning);
        }

        private void ShowBuildTarget()
        {
            EditorGUILayout.LabelField("ビルド対象:");

            using (new EditorGUI.IndentLevelScope())
            {
                TargetOS? selectedOS = null;
                if (_selectedTargetOS.Any(kvp => kvp.Value))
                    selectedOS = _selectedTargetOS.First(kvp => kvp.Value).Key;

                foreach (var targetOS in _selectedTargetOS.Keys.ToArray())
                {
                    _selectedTargetOS[targetOS] = GUILayout.Toggle(_selectedTargetOS[targetOS], targetOS.ToString());

                    // 違うターゲットが選択されたら、前のターゲットは選択解除する
                    if (_selectedTargetOS[targetOS] && selectedOS.HasValue && selectedOS.Value != targetOS)
                        _selectedTargetOS[selectedOS.Value] = false;
                }
            }

            if (!_selectedTargetOS.Any(kvp => kvp.Value))
                EditorGUILayout.HelpBox("出力するOSを選択してください", MessageType.Warning);
        }

        private static void BuildBundle(string packageName, string outputFolder, HashSet<string> contentNames,
            TargetOS targetOS)
        {
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            var outputPath = $"{outputFolder}/{packageName}";
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, recursive: true);

            Directory.CreateDirectory(outputPath);

            // まず全てビルドする
            var buildTarget = targetOS switch
            {
                TargetOS.Windows => BuildTarget.StandaloneWindows,
                TargetOS.OSX => BuildTarget.StandaloneOSX,
                TargetOS.Android => BuildTarget.Android,
                TargetOS.iOS => BuildTarget.iOS,
                _ => throw new ArgumentOutOfRangeException(nameof(targetOS), targetOS, null)
            };
            BuildPipeline.BuildAssetBundles(outputPath, BuildAssetBundleOptions.None, buildTarget);

            // パッケージに ContentsTable と VersionManifest を含める
            var rootBundle = AssetBundle.LoadFromFile($"{outputPath}/{packageName}");
            var rootManifest = rootBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            PackageAttachmentBuilder.ExportAssetBundleContentsTable(outputPath, contentNames, rootManifest);
            PackageAttachmentBuilder.ExportVersionManifest(outputPath, contentNames, rootManifest);

            // 出力された root バンドルはフォルダ名と同じになってるので、プロジェクトの root バンドル名にもどしておく
            var rootBundleName = $"{outputPath}/{packageName}";
            var rootBundleManifestName = $"{outputPath}/{packageName}.manifest";
            File.Move(rootBundleName, $"{outputPath}/{AssetLoaderSetting.RootBundleName}");
            File.Move(rootBundleManifestName, $"{outputPath}/{AssetLoaderSetting.RootBundleName}.manifest");

            // contentNames に含まれないバンドルは削除する
            foreach (var fileName in Directory.GetFiles(outputPath))
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                if (fileNameWithoutExtension == AssetLoaderSetting.RootBundleName)
                    continue;

                if (contentNames.Contains(fileNameWithoutExtension))
                    continue;

                if (fileNameWithoutExtension == Path.GetFileNameWithoutExtension(AssetLoaderSetting.ContentsTableName) ||
                    fileNameWithoutExtension == Path.GetFileNameWithoutExtension(AssetLoaderSetting.VersionManifestFileName))
                    continue;

                File.Delete(fileName);
            }

            AssetDatabase.Refresh();
        }
    }
}