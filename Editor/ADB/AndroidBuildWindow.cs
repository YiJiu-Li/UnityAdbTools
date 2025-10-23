/*
* 文件名：AndroidBuildWindow.cs
* 作者：依旧
* 版本：1.0
* Unity版本：2021.3.26f1
* 创建日期：2024/07/10 14:08:56
* 版权：© 2024 杭州西雨动画有限公司
* All rights reserved.
*/

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YZJ
{
    // 移除AppInfo，改为ScriptableObject模板

    /// <summary>
    /// 类：AndroidBuildWindow
    /// 描述：此类用于在Unity中切换应用名称和包名，并执行Android打包操作，同时动态选择场景进行打包。
    /// </summary>
    public class AndroidBuildWindow : EditorWindow
    {
        private static string versionNumber = "1.0.0"; // 版本号
        private static int versionCode = 1; // 版本代码

        private List<AndroidBuildTemplate> templates; // 模板列表
        private int selectedTemplateIndex = 0; // 当前选择的模板索引
        private List<EditorBuildSettingsScene> sceneList; // 当前打包的场景列表

        [MenuItem("依旧/Android开发/应用构建窗口 %&b", false, 20)]
        public static void ShowWindow()
        {
            GetWindow<AndroidBuildWindow>("Android构建窗口");
        }

        [MenuItem("依旧/Android开发/创建默认构建模板", false, 21)]
        public static void CreateDefaultTemplateMenuItem()
        {
            CreateDefaultTemplate();
            ShowWindow(); // 打开窗口
        }

        private void OnEnable()
        {
            // 加载所有模板资源
            // 首先按类型搜索（优先），注意模块目录可能存在不同路径
            string[] searchPaths = new[]
            {
                "Assets/Framework/Editor/UnityAdbTools/Editor/ADB/Templates",
                "Assets/Framework/Editor/ADB/Templates",
                "Assets/Framework/Editor/ADB",
                "Assets/Framework/Editor/UnityAdbTools/Editor/ADB",
            };

            templates = new List<AndroidBuildTemplate>();

            // 尝试按类型查找
            string[] guids = AssetDatabase.FindAssets("t:AndroidBuildTemplate");
            if (guids != null && guids.Length > 0)
            {
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var template = AssetDatabase.LoadAssetAtPath<AndroidBuildTemplate>(path);
                    if (template != null)
                        templates.Add(template);
                }
            }

            // 如果按类型查找失败，则尝试在常见 Templates 目录下枚举 .asset 文件作为回退
            if (templates.Count == 0)
            {
                foreach (var dir in searchPaths)
                {
                    if (!AssetDatabase.IsValidFolder(dir))
                        continue;

                    var assetPaths = Directory.GetFiles(
                        dir,
                        "*.asset",
                        SearchOption.TopDirectoryOnly
                    );
                    foreach (var assetPath in assetPaths)
                    {
                        // AssetDatabase 使用相对路径且使用正斜杠
                        string relativePath = assetPath.Replace("\\", "/");
                        // Ensure path starts with Assets/
                        int idx = relativePath.IndexOf("Assets/");
                        if (idx >= 0)
                            relativePath = relativePath.Substring(idx);

                        var template = AssetDatabase.LoadAssetAtPath<AndroidBuildTemplate>(
                            relativePath
                        );
                        if (template != null && !templates.Contains(template))
                            templates.Add(template);
                    }

                    if (templates.Count > 0)
                        break;
                }
            }
            if (templates.Count == 0)
            {
                Debug.LogWarning("未找到任何Android Build模板，请先创建！");
            }
            selectedTemplateIndex = 0;

            // 加载当前版本信息
            LoadVersionInfo();

            // 加载当前打包场景
            sceneList = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        }

        private void OnGUI()
        {
            GUILayout.Label("Android 打包模板设置", EditorStyles.boldLabel);

            if (templates == null || templates.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "未找到任何模板，请在Templates文件夹创建Android Build Template资源。",
                    MessageType.Warning
                );
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("刷新模板列表"))
                {
                    OnEnable();
                }
                if (GUILayout.Button("创建默认模板"))
                {
                    CreateDefaultTemplate();
                    OnEnable(); // 创建后重新加载模板列表
                }
                GUILayout.EndHorizontal();
                return;
            }

            // 模板选择
            string[] templateNames = templates
                .Select(t => t.appName + " (" + t.packageName + ")")
                .ToArray();
            selectedTemplateIndex = EditorGUILayout.Popup(
                "选择模板:",
                selectedTemplateIndex,
                templateNames
            );
            var currentTemplate = templates[selectedTemplateIndex];

            GUILayout.Space(10);
            GUILayout.Label("模板参数", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("应用名称:", currentTemplate.appName);
            EditorGUILayout.LabelField("包名:", currentTemplate.packageName);
            EditorGUILayout.LabelField("输出目录:", currentTemplate.outputDirectory);
            EditorGUILayout.LabelField("APK文件名格式:", currentTemplate.apkNameFormat);
            versionNumber = EditorGUILayout.TextField("版本号:", versionNumber);
            GUILayout.Label("版本代码: " + versionCode);

            GUILayout.Space(10);
            GUILayout.Label("选择要打包的场景", EditorStyles.boldLabel);
            for (int i = 0; i < sceneList.Count; i++)
            {
                sceneList[i].enabled = EditorGUILayout.ToggleLeft(
                    sceneList[i].path,
                    sceneList[i].enabled
                );
            }

            GUILayout.Space(10);
            if (GUILayout.Button("应用模板参数（设置应用名称和包名）"))
            {
                SetPackageAndAppName(currentTemplate);
            }

            GUILayout.Space(10);
            if (GUILayout.Button("打包Android"))
            {
                SaveSceneSettings();
                BuildAndroid(BuildOptions.None, currentTemplate);
            }
            if (GUILayout.Button("打包Android Build And Run"))
            {
                SaveSceneSettings();
                BuildAndroid(BuildOptions.AutoRunPlayer, currentTemplate);
            }

            // 添加底部分隔线
            EditorGUILayout.Space();
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
            EditorGUILayout.Space();

            // 添加作者信息
            GUIStyle footerStyle = new GUIStyle(EditorStyles.miniLabel);
            footerStyle.alignment = TextAnchor.MiddleCenter;
            footerStyle.normal.textColor = new Color(0.5f, 0.5f, 0.8f);

            if (GUILayout.Button("作者:依旧 | GitHub: https://github.com/YiJiu-Li", footerStyle))
            {
                Application.OpenURL("https://github.com/YiJiu-Li");
            }
        }

        // 设置包名和应用名称（模板版）
        private void SetPackageAndAppName(AndroidBuildTemplate template)
        {
            PlayerSettings.productName = template.appName;
            Debug.Log("应用名称设置为: " + template.appName);
            PlayerSettings.applicationIdentifier = template.packageName;
            Debug.Log("包名设置为: " + template.packageName);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("设置成功", "应用名称和包名已成功设置！", "确定");
        }

        // 保存场景激活状态
        private void SaveSceneSettings()
        {
            EditorBuildSettings.scenes = sceneList.ToArray();
            Debug.Log("场景设置已更新");
        }

        // 移除GetAppNames

        // 执行打包操作（模板版）
        private void BuildAndroid(BuildOptions buildOptions, AndroidBuildTemplate template)
        {
            LoadVersionInfo();
            string[] scenes = GetScenesToBuild();
            bool shouldBuild = ShowBuildInfo(scenes, template);
            if (shouldBuild)
            {
                PerformBuild(scenes, buildOptions, template);
            }
        }

        // 获取所有需要打包的场景
        private static string[] GetScenesToBuild()
        {
            return EditorBuildSettings
                .scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
        }

        // 执行打包操作（模板版）
        private void PerformBuild(
            string[] scenes,
            BuildOptions buildOptions,
            AndroidBuildTemplate template
        )
        {
            PlayerSettings.bundleVersion = versionNumber;
            PlayerSettings.Android.bundleVersionCode = versionCode;

            // 确保打包输出目录存在
            string buildPath = template.outputDirectory;
            if (!Directory.Exists(buildPath))
            {
                Directory.CreateDirectory(buildPath);
            }

            // 生成apk文件名
            string apkName = template
                .apkNameFormat.Replace("{appName}", template.appName)
                .Replace("{version}", versionNumber)
                .Replace("{versionCode}", versionCode.ToString());

            // 配置打包选项
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(buildPath, apkName),
                target = BuildTarget.Android,
                options = buildOptions,
            };

            // TODO: 可扩展签名等参数

            BuildPipeline.BuildPlayer(buildPlayerOptions);
            IncrementVersionNumber();
            SaveVersionInfo();
            Debug.Log("打包成功！");
        }

        // 增加版本号
        private static void IncrementVersionNumber()
        {
            // 简单的版本号递增逻辑
            string[] parts = versionNumber.Split('.');
            int buildNumber;
            if (int.TryParse(parts[2], out buildNumber))
            {
                buildNumber++;
                // versionNumber = $"{parts[0]}.{parts[1]}.{buildNumber}";
            }

            // 增加版本代码
            versionCode++;
        }

        // 保存版本信息到文件
        private static void SaveVersionInfo()
        {
            System.IO.File.WriteAllText("Assets/version.txt", $"{versionNumber}\n{versionCode}");
        }

        // 从文件加载版本信息
        private static void LoadVersionInfo()
        {
            if (System.IO.File.Exists("Assets/version.txt"))
            {
                string[] lines = System.IO.File.ReadAllLines("Assets/version.txt");
                if (lines.Length >= 2)
                {
                    // versionNumber = lines[0];
                    versionCode = int.Parse(lines[1]);
                }
            }
        }

        /// <summary>
        /// 创建一个默认的Android打包模板
        /// </summary>
        private static void CreateDefaultTemplate()
        {
            // 确保目录存在
            string templateDir = "Assets/Framework/Editor/UnityAdbTools/Editor/ADB/Templates";
            if (!AssetDatabase.IsValidFolder(templateDir))
            {
                // 递归创建目录
                string[] pathParts = templateDir.Split('/');
                string currentPath = pathParts[0]; // Assets
                for (int i = 1; i < pathParts.Length; i++)
                {
                    string folderName = pathParts[i];
                    string parentPath = currentPath;
                    currentPath = Path.Combine(currentPath, folderName);

                    if (!AssetDatabase.IsValidFolder(currentPath))
                    {
                        string guid = AssetDatabase.CreateFolder(parentPath, folderName);
                        if (string.IsNullOrEmpty(guid))
                        {
                            Debug.LogError($"无法创建目录: {currentPath}");
                            return;
                        }
                    }
                }
            }

            // 创建默认模板
            string assetPath = $"{templateDir}/DefaultTemplate.asset";

            // 检查文件是否已存在
            if (AssetDatabase.LoadAssetAtPath<AndroidBuildTemplate>(assetPath) != null)
            {
                bool replace = EditorUtility.DisplayDialog(
                    "模板已存在",
                    "默认模板文件已存在，是否替换？",
                    "替换",
                    "取消"
                );

                if (!replace)
                {
                    return;
                }
            }

            // 创建ScriptableObject
            var template = ScriptableObject.CreateInstance<AndroidBuildTemplate>();
            template.appName = "默认应用名称";
            template.packageName = "com.company.default";
            template.outputDirectory = "Builds/Android";
            template.apkNameFormat = "{appName}_v{version}_{versionCode}.apk";

            // 如果是覆盖现有资源
            if (File.Exists(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            // 保存资源
            AssetDatabase.CreateAsset(template, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 在Project窗口中选中新创建的模板
            var newTemplate = AssetDatabase.LoadAssetAtPath<AndroidBuildTemplate>(assetPath);
            if (newTemplate != null)
            {
                Selection.activeObject = newTemplate;
                EditorUtility.FocusProjectWindow();
            }

            Debug.Log($"已创建默认Android打包模板: {assetPath}");
            EditorUtility.DisplayDialog("创建成功", $"已在 {templateDir} 创建默认模板！", "确定");
        }

        // 显示打包信息弹窗（模板版）
        private bool ShowBuildInfo(string[] scenes, AndroidBuildTemplate template)
        {
            string sceneList = string.Join("\n", scenes);
            string apkName = template
                .apkNameFormat.Replace("{appName}", template.appName)
                .Replace("{version}", versionNumber)
                .Replace("{versionCode}", versionCode.ToString());
            return EditorUtility.DisplayDialog(
                "打包信息",
                $"模板: {template.appName} ({template.packageName})\n"
                    + $"输出目录: {template.outputDirectory}\n"
                    + $"APK文件名: {apkName}\n\n"
                    + $"以下场景将被打包:\n{sceneList}\n\n"
                    + $"版本号: {versionNumber}\n"
                    + $"版本代码: {versionCode}\n\n"
                    + "是否继续进行打包？",
                "是",
                "否"
            );
        }
    }
}
