using UnityEngine;

namespace YZJ
{
    [CreateAssetMenu(
        fileName = "AndroidBuildTemplate",
        menuName = "Build/Android Build Template",
        order = 1
    )]
    public class AndroidBuildTemplate : ScriptableObject
    {
        public string appName = "应用名称";
        public string packageName = "com.company.app";
        public string outputDirectory = "Builds";
        public string apkNameFormat = "{appName}_v{version}_{versionCode}.apk";
        public string keystorePath = "";
        public string keystorePassword = "";
        public string keyAlias = "";
        public string keyPassword = "";
        // 可扩展更多参数
    }
}
