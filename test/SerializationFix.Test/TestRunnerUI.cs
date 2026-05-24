using System.Collections.Generic;
using KSP.UI.Screens;
using UnityEngine;

namespace KSPSerializationFix.Test;

// Pops up an applauncher button on the main menu. Clicking it runs the
// instantiate round-trip test against TestPayloadHost and shows pass/fail
// for each field in a small GUI window.
[KSPAddon(KSPAddon.Startup.MainMenu, false)]
public class TestRunnerUI : MonoBehaviour
{
    ApplicationLauncherButton button;
    List<CheckResult> results;
    bool showWindow;
    Rect windowRect = new Rect(100, 100, 480, 360);
    Vector2 scroll;

    static Texture2D buttonTexture;

    void Start()
    {
        if (buttonTexture == null)
        {
            buttonTexture = new Texture2D(38, 38, TextureFormat.RGBA32, false);
            var pixels = new Color32[38 * 38];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0, 180, 80, 255);
            buttonTexture.SetPixels32(pixels);
            buttonTexture.Apply(false, true);
        }

        GameEvents.onGUIApplicationLauncherReady.Add(OnAppLauncherReady);
        if (ApplicationLauncher.Ready)
            OnAppLauncherReady();
    }

    void OnDestroy()
    {
        GameEvents.onGUIApplicationLauncherReady.Remove(OnAppLauncherReady);
        if (button != null && ApplicationLauncher.Instance != null)
            ApplicationLauncher.Instance.RemoveModApplication(button);
    }

    void OnAppLauncherReady()
    {
        if (button != null)
            return;

        button = ApplicationLauncher.Instance.AddModApplication(
            OnButtonTrue,
            OnButtonFalse,
            null,
            null,
            null,
            null,
            ApplicationLauncher.AppScenes.MAINMENU,
            buttonTexture
        );
    }

    void OnButtonTrue()
    {
        RunTest();
        showWindow = true;
    }

    void OnButtonFalse() => showWindow = false;

    void RunTest()
    {
        results = new List<CheckResult>();

        var original = ScriptableObject.CreateInstance<TestPayloadHost>();
        original.payload = new TestPayload
        {
            answer = 42,
            text = "the quick brown fox",
            ratio = 3.14159f,
            flag = true,
        };
        original.sentinel = 12345;

        TestPayloadHost clone = null;
        try
        {
            clone = Object.Instantiate(original);
        }
        catch (System.Exception ex)
        {
            results.Add(CheckResult.Fail("Object.Instantiate threw", ex.ToString()));
            Object.Destroy(original);
            LogSummary();
            return;
        }

        results.Add(
            CheckResult.From("sentinel (control field on host)", original.sentinel, clone.sentinel)
        );
        results.Add(
            CheckResult.From("payload.answer", original.payload.answer, clone.payload.answer)
        );
        results.Add(CheckResult.From("payload.text", original.payload.text, clone.payload.text));
        results.Add(CheckResult.From("payload.ratio", original.payload.ratio, clone.payload.ratio));
        results.Add(CheckResult.From("payload.flag", original.payload.flag, clone.payload.flag));

        Object.Destroy(clone);
        Object.Destroy(original);

        LogSummary();
    }

    void LogSummary()
    {
        int pass = 0;
        int fail = 0;
        foreach (var r in results)
        {
            if (r.passed)
                pass++;
            else
                fail++;
        }
        Debug.Log($"[SerializationFix.Test] {pass} passed, {fail} failed ({results.Count} total)");
        foreach (var r in results)
        {
            if (!r.passed)
                Debug.LogError($"[SerializationFix.Test] FAIL: {r.name} -- {r.detail}");
        }
    }

    void OnGUI()
    {
        if (!showWindow || results == null)
            return;

        GUI.skin = HighLogic.Skin;
        Styles.Init();
        windowRect = GUILayout.Window(
            GetInstanceID(),
            windowRect,
            DrawWindow,
            "SerializationFix Test"
        );
    }

    void DrawWindow(int id)
    {
        int pass = 0;
        int fail = 0;
        foreach (var r in results)
        {
            if (r.passed)
                pass++;
            else
                fail++;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label(
            $"{pass} passed, {fail} failed",
            fail > 0 ? Styles.failLabel : Styles.passLabel
        );
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Re-run"))
            RunTest();
        if (GUILayout.Button("Close"))
        {
            showWindow = false;
            button?.SetFalse(false);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        scroll = GUILayout.BeginScrollView(scroll);
        foreach (var r in results)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(
                r.passed ? "PASS" : "FAIL",
                r.passed ? Styles.passLabel : Styles.failLabel,
                GUILayout.Width(48)
            );
            GUILayout.Label(r.name);
            GUILayout.EndHorizontal();
            if (!r.passed)
                GUILayout.Label("    " + r.detail, Styles.detailLabel);
        }
        GUILayout.EndScrollView();

        GUI.DragWindow();
    }

    struct CheckResult
    {
        public string name;
        public bool passed;
        public string detail;

        public static CheckResult From<T>(string name, T expected, T actual)
        {
            bool eq = EqualityComparer<T>.Default.Equals(expected, actual);
            return new CheckResult
            {
                name = name,
                passed = eq,
                detail = eq ? null : $"expected '{expected}', got '{actual}'",
            };
        }

        public static CheckResult Fail(string name, string detail) =>
            new CheckResult
            {
                name = name,
                passed = false,
                detail = detail,
            };
    }

    static class Styles
    {
        static bool initialized;
        internal static GUIStyle passLabel;
        internal static GUIStyle failLabel;
        internal static GUIStyle detailLabel;

        internal static void Init()
        {
            if (initialized)
                return;
            initialized = true;
            passLabel = new GUIStyle(HighLogic.Skin.label) { normal = { textColor = Color.green } };
            failLabel = new GUIStyle(HighLogic.Skin.label) { normal = { textColor = Color.red } };
            detailLabel = new GUIStyle(HighLogic.Skin.label)
            {
                normal = { textColor = new Color(1f, 0.8f, 0.4f) },
                wordWrap = true,
                fontSize = 11,
            };
        }
    }
}
