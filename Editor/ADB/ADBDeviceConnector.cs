using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ADB设备连接器
/// 用于在Unity编辑器中连接Android设备，方便调试和测试
/// </summary>
public class ADBDeviceConnector : EditorWindow
{
    // 配置键
    private const string ADB_PATH_PREF_KEY = "ADBDeviceConnector_AdbPath";
    private const string DEVICE_IP_PREF_KEY = "ADBDeviceConnector_DeviceIP";

    // UI状态
    private string adbPath = "";
    private string deviceIP = "";
    private bool isProcessing = false;
    private string statusMessage = "";
    private bool showAdvancedOptions = false;
    private Vector2 scrollPosition;
    private string deviceListText = "";
    private List<string> connectedDevices = new List<string>();
    private int selectedDeviceIndex = -1;
    private int adbPort = 5555;
    private bool autoRefresh = true;
    private double lastRefreshTime;
    private const double AUTO_REFRESH_INTERVAL = 5.0; // 5秒自动刷新

    // 日志
    private List<string> logMessages = new List<string>();
    private bool showLogs = false;

    [MenuItem("ADB/ADB设备连接器 %&a")] // 添加快捷键 Ctrl+Alt+A
    public static void ShowWindow()
    {
        GetWindow<ADBDeviceConnector>("ADB设备连接器");
    }

    private void OnEnable()
    {
        // 从EditorPrefs加载保存的设置
        adbPath = EditorPrefs.GetString(ADB_PATH_PREF_KEY, "");
        deviceIP = EditorPrefs.GetString(DEVICE_IP_PREF_KEY, "");

        if (string.IsNullOrEmpty(adbPath))
        {
            adbPath = GetAdbPath();
        }

        // 初始化加载设备列表
        RefreshDeviceList();
    }

    private void OnGUI()
    {
        // 如果正在处理中，显示进度条
        if (isProcessing)
        {
            EditorGUI.ProgressBar(new Rect(0, 0, position.width, 20), 0.5f, "处理中...");
            EditorGUILayout.Space(25);
        }

        // 状态信息显示
        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawADBPathSection();
        EditorGUILayout.Space();
        DrawDeviceConnectionSection();
        EditorGUILayout.Space();
        DrawDeviceListSection();
        EditorGUILayout.Space();
        DrawAdvancedOptionsSection();
        EditorGUILayout.Space();
        DrawLogsSection();

        EditorGUILayout.EndScrollView();

        // 自动刷新设备列表
        if (
            autoRefresh
            && EditorApplication.timeSinceStartup - lastRefreshTime > AUTO_REFRESH_INTERVAL
        )
        {
            RefreshDeviceList();
            lastRefreshTime = EditorApplication.timeSinceStartup;
            Repaint();
        }
    }

    private void DrawADBPathSection()
    {
        EditorGUILayout.LabelField("ADB配置", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("ADB路径：", GUILayout.Width(80));
        string newAdbPath = EditorGUILayout.TextField(adbPath);
        if (newAdbPath != adbPath)
        {
            adbPath = newAdbPath;
            EditorPrefs.SetString(ADB_PATH_PREF_KEY, adbPath);
        }

        if (GUILayout.Button("浏览", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFilePanel("选择ADB可执行文件", "", "exe");
            if (!string.IsNullOrEmpty(path))
            {
                adbPath = path;
                EditorPrefs.SetString(ADB_PATH_PREF_KEY, adbPath);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("验证ADB路径"))
        {
            ValidateADBPath();
        }
        if (GUILayout.Button("重启ADB服务"))
        {
            RestartADBServer();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDeviceConnectionSection()
    {
        EditorGUILayout.LabelField("设备连接", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("设备IP地址：", GUILayout.Width(80));
        string newDeviceIP = EditorGUILayout.TextField(deviceIP);
        if (newDeviceIP != deviceIP)
        {
            deviceIP = newDeviceIP;
            EditorPrefs.SetString(DEVICE_IP_PREF_KEY, deviceIP);
        }

        adbPort = EditorGUILayout.IntField("端口：", adbPort, GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("检查设备IP"))
        {
            CheckDeviceIP();
        }
        if (GUILayout.Button("连接设备"))
        {
            ConnectDevice();
        }
        if (GUILayout.Button("断开所有设备"))
        {
            DisconnectAllDevices();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDeviceListSection()
    {
        EditorGUILayout.LabelField("设备列表", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新设备列表"))
        {
            RefreshDeviceList();
        }
        autoRefresh = EditorGUILayout.Toggle("自动刷新", autoRefresh);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (connectedDevices.Count > 0)
        {
            selectedDeviceIndex = EditorGUILayout.Popup(
                "选择设备:",
                selectedDeviceIndex,
                connectedDevices.ToArray()
            );

            if (selectedDeviceIndex >= 0 && selectedDeviceIndex < connectedDevices.Count)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("断开选中设备"))
                {
                    DisconnectDevice(connectedDevices[selectedDeviceIndex]);
                }
                if (GUILayout.Button("检查设备状态"))
                {
                    CheckDeviceState(connectedDevices[selectedDeviceIndex]);
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("未检测到已连接的设备", MessageType.Info);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("设备输出:");
        EditorGUILayout.TextArea(deviceListText, GUILayout.Height(100));
    }

    private void DrawAdvancedOptionsSection()
    {
        EditorGUI.indentLevel++;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("安装APK"))
        {
            InstallAPK();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUI.indentLevel--;
    }

    private void DrawLogsSection()
    {
        showLogs = EditorGUILayout.Foldout(showLogs, "操作日志");

        if (showLogs && logMessages.Count > 0)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("清空日志"))
            {
                logMessages.Clear();
            }
            if (GUILayout.Button("复制日志"))
            {
                CopyLogs();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            foreach (string log in logMessages)
            {
                EditorGUILayout.LabelField(log, EditorStyles.wordWrappedLabel);
            }

            EditorGUI.indentLevel--;
        }
    }

    /// <summary>
    /// 自动获取ADB路径
    /// </summary>
    public string GetAdbPath()
    {
        string sdkPath = EditorPrefs.GetString("AndroidSdkRoot");
        if (string.IsNullOrEmpty(sdkPath))
        {
            AddLog(
                "错误: Android SDK 路径未设置。请在 Unity 编辑器中设置 Android SDK 路径 (Edit > Preferences > External Tools)。",
                true
            );
            return "";
        }

        string adbPath = Path.Combine(sdkPath, "platform-tools", "adb.exe");
        if (File.Exists(adbPath))
        {
            AddLog("已找到ADB路径: " + adbPath);
            return adbPath;
        }
        else
        {
            AddLog("错误: 未找到 ADB 可执行文件，请确认 Android SDK 安装是否正确。", true);
            return "";
        }
    }

    /// <summary>
    /// 验证ADB路径是否有效
    /// </summary>
    private void ValidateADBPath()
    {
        if (string.IsNullOrEmpty(adbPath))
        {
            ShowError("请填写ADB路径。");
            return;
        }

        if (!File.Exists(adbPath))
        {
            ShowError("ADB可执行文件不存在，请检查路径。");
            return;
        }

        try
        {
            string versionOutput = RunADBCommand("version");
            if (!string.IsNullOrEmpty(versionOutput))
            {
                ShowSuccess("ADB路径有效: " + versionOutput.Split('\n')[0]);
            }
            else
            {
                ShowError("无法获取ADB版本信息。");
            }
        }
        catch (System.Exception ex)
        {
            ShowError("验证ADB路径出错: " + ex.Message);
        }
    }

    /// <summary>
    /// 重新启动ADB服务
    /// </summary>
    private void RestartADBServer()
    {
        if (string.IsNullOrEmpty(adbPath))
        {
            ShowError("请填写ADB路径。");
            return;
        }

        try
        {
            isProcessing = true;
            statusMessage = "正在重启ADB服务...";
            Repaint();

            Task.Run(() =>
            {
                RunADBCommand("kill-server");
                RunADBCommand("start-server");

                EditorApplication.delayCall += () =>
                {
                    isProcessing = false;
                    RefreshDeviceList();
                    ShowSuccess("ADB服务已重启。");
                };
            });
        }
        catch (System.Exception ex)
        {
            isProcessing = false;
            ShowError("重启ADB服务出错: " + ex.Message);
        }
    }

    /// <summary>
    /// 检查设备IP
    /// </summary>
    private void CheckDeviceIP()
    {
        if (string.IsNullOrEmpty(adbPath))
        {
            ShowError("请填写ADB路径。");
            return;
        }

        try
        {
            isProcessing = true;
            statusMessage = "正在检查设备IP...";
            Repaint();

            Task.Run(() =>
            {
                // 尝试不同的网络接口获取IP
                string[] networkInterfaces = { "wlan0", "eth0", "eth1" };
                bool ipFound = false;

                foreach (string networkInterface in networkInterfaces)
                {
                    string ipOutput = RunADBCommand(
                        $"shell ip -f inet addr show {networkInterface}"
                    );
                    if (!string.IsNullOrEmpty(ipOutput))
                    {
                        string ip = ParseIPFromOutput(ipOutput);
                        if (!string.IsNullOrEmpty(ip))
                        {
                            ipFound = true;

                            EditorApplication.delayCall += () =>
                            {
                                deviceIP = ip;
                                EditorPrefs.SetString(DEVICE_IP_PREF_KEY, deviceIP);
                                isProcessing = false;
                                ShowSuccess($"设备IP地址 ({networkInterface}): {deviceIP}");
                            };

                            break;
                        }
                    }
                }

                if (!ipFound)
                {
                    EditorApplication.delayCall += () =>
                    {
                        isProcessing = false;
                        ShowError("无法获取设备IP地址，请确保设备已连接并已启用USB调试。");
                    };
                }
            });
        }
        catch (System.Exception ex)
        {
            isProcessing = false;
            ShowError("检查设备IP出错: " + ex.Message);
        }
    }

    /// <summary>
    /// 从命令输出中解析IP地址
    /// </summary>
    private string ParseIPFromOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
            return null;

        string[] lines = output.Split('\n');
        foreach (string line in lines)
        {
            if (line.Trim().StartsWith("inet "))
            {
                try
                {
                    // 提取IP地址 (inet 192.168.1.100/24 brd 192.168.1.255 scope global wlan0)
                    string ip = line.Trim().Split(' ')[1].Split('/')[0];
                    // 验证IP格式
                    if (
                        System.Text.RegularExpressions.Regex.IsMatch(
                            ip,
                            @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$"
                        )
                    )
                    {
                        return ip;
                    }
                }
                catch
                {
                    // 解析失败，继续查找
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 连接到设备
    /// </summary>
    private void ConnectDevice()
    {
        if (string.IsNullOrEmpty(adbPath))
        {
            ShowError("请填写ADB路径。");
            return;
        }

        if (string.IsNullOrEmpty(deviceIP))
        {
            ShowError("请填写设备IP地址。");
            return;
        }

        try
        {
            isProcessing = true;
            statusMessage = $"正在连接到 {deviceIP}:{adbPort}...";
            Repaint();

            Task.Run(() =>
            {
                RunADBCommand("disconnect");
                RunADBCommand($"tcpip {adbPort}");
                string connectOutput = RunADBCommand($"connect {deviceIP}:{adbPort}");

                EditorApplication.delayCall += () =>
                {
                    isProcessing = false;

                    if (connectOutput != null && connectOutput.Contains("connected"))
                    {
                        ShowSuccess($"已成功连接到设备 {deviceIP}:{adbPort}");
                        RefreshDeviceList();
                    }
                    else
                    {
                        ShowError($"连接设备失败: {connectOutput}");
                    }
                };
            });
        }
        catch (System.Exception ex)
        {
            isProcessing = false;
            ShowError("连接设备出错: " + ex.Message);
        }
    }

    /// <summary>
    /// 刷新设备列表
    /// </summary>
    private void RefreshDeviceList()
    {
        if (string.IsNullOrEmpty(adbPath))
        {
            ShowError("请填写ADB路径。");
            return;
        }

        try
        {
            Task.Run(() =>
            {
                string devicesOutput = RunADBCommand("devices -l");

                EditorApplication.delayCall += () =>
                {
                    deviceListText = devicesOutput;
                    ParseConnectedDevices(devicesOutput);
                    Repaint();
                };
            });
        }
        catch (System.Exception ex)
        {
            ShowError("刷新设备列表出错: " + ex.Message);
        }
    }

    /// <summary>
    /// 解析已连接的设备列表
    /// </summary>
    private void ParseConnectedDevices(string output)
    {
        connectedDevices.Clear();
        selectedDeviceIndex = -1;

        if (string.IsNullOrEmpty(output))
            return;

        string[] lines = output.Split('\n');

        // 跳过第一行 (List of devices attached)
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (!string.IsNullOrEmpty(line))
            {
                string deviceId = line.Split(' ')[0];
                if (!string.IsNullOrEmpty(deviceId) && !deviceId.Contains("List"))
                {
                    connectedDevices.Add(deviceId);
                }
            }
        }

        if (connectedDevices.Count > 0)
        {
            selectedDeviceIndex = 0;
        }
    }

    /// <summary>
    /// 断开所有设备连接
    /// </summary>
    private void DisconnectAllDevices()
    {
        if (string.IsNullOrEmpty(adbPath))
        {
            ShowError("请填写ADB路径。");
            return;
        }

        try
        {
            isProcessing = true;
            statusMessage = "正在断开所有设备...";
            Repaint();

            Task.Run(() =>
            {
                RunADBCommand("disconnect");

                EditorApplication.delayCall += () =>
                {
                    isProcessing = false;
                    ShowSuccess("所有设备已断开连接。");
                    RefreshDeviceList();
                };
            });
        }
        catch (System.Exception ex)
        {
            isProcessing = false;
            ShowError("断开设备出错: " + ex.Message);
        }
    }

    /// <summary>
    /// 断开指定设备连接
    /// </summary>
    private void DisconnectDevice(string deviceId)
    {
        if (string.IsNullOrEmpty(adbPath) || string.IsNullOrEmpty(deviceId))
        {
            ShowError("ADB路径或设备ID无效。");
            return;
        }

        try
        {
            isProcessing = true;
            statusMessage = $"正在断开设备 {deviceId}...";
            Repaint();

            Task.Run(() =>
            {
                RunADBCommand($"disconnect {deviceId}");

                EditorApplication.delayCall += () =>
                {
                    isProcessing = false;
                    ShowSuccess($"设备 {deviceId} 已断开连接。");
                    RefreshDeviceList();
                };
            });
        }
        catch (System.Exception ex)
        {
            isProcessing = false;
            ShowError("断开设备出错: " + ex.Message);
        }
    }

    /// <summary>
    /// 安装APK到设备
    /// </summary>
    private void InstallAPK()
    {
        if (string.IsNullOrEmpty(adbPath))
        {
            ShowError("请填写ADB路径。");
            return;
        }

        string apkPath = EditorUtility.OpenFilePanel("选择APK文件", "", "apk");
        if (string.IsNullOrEmpty(apkPath))
            return;

        try
        {
            isProcessing = true;
            statusMessage = "正在安装APK...";
            Repaint();

            Task.Run(() =>
            {
                string output = RunADBCommand($"install -r \"{apkPath}\"");

                EditorApplication.delayCall += () =>
                {
                    isProcessing = false;

                    if (output != null && output.Contains("Success"))
                    {
                        ShowSuccess("APK安装成功。");
                    }
                    else
                    {
                        ShowError("APK安装失败: " + output);
                    }
                };
            });
        }
        catch (System.Exception ex)
        {
            isProcessing = false;
            ShowError("安装APK出错: " + ex.Message);
        }
    }

    /// <summary>
    /// 检查设备状态
    /// </summary>
    private void CheckDeviceState(string deviceId)
    {
        if (string.IsNullOrEmpty(adbPath) || string.IsNullOrEmpty(deviceId))
        {
            ShowError("ADB路径或设备ID无效。");
            return;
        }

        try
        {
            isProcessing = true;
            statusMessage = $"正在检查设备 {deviceId} 状态...";
            Repaint();

            Task.Run(() =>
            {
                StringBuilder stateInfo = new StringBuilder();

                string model = RunADBCommand($"-s {deviceId} shell getprop ro.product.model")
                    .Trim();
                string version = RunADBCommand(
                        $"-s {deviceId} shell getprop ro.build.version.release"
                    )
                    .Trim();
                string manufacturer = RunADBCommand(
                        $"-s {deviceId} shell getprop ro.product.manufacturer"
                    )
                    .Trim();
                string batteryInfo = RunADBCommand($"-s {deviceId} shell dumpsys battery");

                stateInfo.AppendLine($"设备: {manufacturer} {model}");
                stateInfo.AppendLine($"Android版本: {version}");

                // 解析电池信息
                if (!string.IsNullOrEmpty(batteryInfo))
                {
                    foreach (string line in batteryInfo.Split('\n'))
                    {
                        if (line.Contains("level:"))
                        {
                            string level = line.Trim().Split(':')[1].Trim();
                            stateInfo.AppendLine($"电池电量: {level}%");
                            break;
                        }
                    }
                }

                string stateInfoStr = stateInfo.ToString();

                EditorApplication.delayCall += () =>
                {
                    isProcessing = false;
                    deviceListText = stateInfoStr;
                    ShowSuccess("设备状态已更新。");
                };
            });
        }
        catch (System.Exception ex)
        {
            isProcessing = false;
            ShowError("检查设备状态出错: " + ex.Message);
        }
    }

    /// <summary>
    /// 执行ADB命令
    /// </summary>
    private string RunADBCommand(string arguments)
    {
        try
        {
            AddLog($"运行ADB命令: {arguments}");

            Process adbProcess = new Process();
            adbProcess.StartInfo.FileName = adbPath;
            adbProcess.StartInfo.Arguments = arguments;
            adbProcess.StartInfo.UseShellExecute = false;
            adbProcess.StartInfo.RedirectStandardOutput = true;
            adbProcess.StartInfo.RedirectStandardError = true;
            adbProcess.StartInfo.CreateNoWindow = true;

            adbProcess.Start();
            string output = adbProcess.StandardOutput.ReadToEnd();
            string errorOutput = adbProcess.StandardError.ReadToEnd();
            adbProcess.WaitForExit();

            if (adbProcess.ExitCode != 0 && !string.IsNullOrEmpty(errorOutput))
            {
                AddLog($"ADB命令错误: {errorOutput}", true);
                return null;
            }

            return output;
        }
        catch (System.Exception ex)
        {
            AddLog($"执行ADB命令异常: {ex.Message}", true);
            return null;
        }
    }

    #region 辅助方法

    /// <summary>
    /// 显示成功消息
    /// </summary>
    private void ShowSuccess(string message)
    {
        statusMessage = message;
        AddLog(message);
        Repaint();
    }

    /// <summary>
    /// 显示错误消息
    /// </summary>
    private void ShowError(string message)
    {
        statusMessage = "错误: " + message;
        AddLog("错误: " + message, true);
        Repaint();
        EditorUtility.DisplayDialog("错误", message, "确定");
    }

    /// <summary>
    /// 添加日志消息
    /// </summary>
    private void AddLog(string message, bool isError = false)
    {
        if (isError)
        {
            UnityEngine.Debug.LogError(message);
        }
        else
        {
            UnityEngine.Debug.Log(message);
        }

        string timeStamp = System.DateTime.Now.ToString("HH:mm:ss");
        logMessages.Add($"[{timeStamp}] {message}");

        // 保持日志列表不会太长
        while (logMessages.Count > 100)
        {
            logMessages.RemoveAt(0);
        }
    }

    /// <summary>
    /// 复制日志到剪贴板
    /// </summary>
    private void CopyLogs()
    {
        if (logMessages.Count == 0)
            return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (string log in logMessages)
        {
            sb.AppendLine(log);
        }

        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        ShowSuccess("日志已复制到剪贴板。");
    }

    #endregion
}
