#if UNITY_ANDROID
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;

public sealed class AndroidReleasePostprocess : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 100;
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifest = Path.Combine(path, "src/main/AndroidManifest.xml");
        var xml = new XmlDocument(); xml.Load(manifest);
        const string android = "http://schemas.android.com/apk/res/android";
        foreach (XmlElement element in xml.SelectNodes("/manifest/uses-permission").Cast<XmlElement>().ToArray())
        {
            string name = element.GetAttribute("name", android);
            if (name == "android.permission.VIBRATE" || (!EditorUserBuildSettings.development && name == "android.permission.INTERNET"))
                xml.DocumentElement.RemoveChild(element);
        }
        XmlElement permission = xml.CreateElement("uses-permission");
        permission.SetAttribute("name", android, "android.permission.VIBRATE");
        xml.DocumentElement.AppendChild(permission);
        xml.Save(manifest);
    }
}
#endif
