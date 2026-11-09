// Настройки игры: звук, графика, камера, управление, интерфейс. Сохраняются в PlayerPrefs.
using UnityEngine;

namespace Hockey
{
    public static class Settings
    {
        public static float Master = 0.8f, Sfx = 1f, Crowd = 0.8f, Music = 0.5f;
        public static int Quality = 2;            // 0 — низкое, 1 — среднее, 2 — высокое
        public static bool Fullscreen = true;
        public static float Fov = 62;
        public static float CamDistance = 1f;     // множитель дистанции камеры
        public static float MouseSens = 1f;
        public static bool InvertY;
        public static bool AutoCamera = true;     // камера сама разворачивается за игроком
        public static bool Hints = true;
        public static bool Names = true;
        public static bool ScreenShake = true;
        public static float UiScale = 1f;
        public static float Brightness = 0.9f;
        public static bool Post = true;

        public static void Load()
        {
            Master = PlayerPrefs.GetFloat("s.master", Master);
            Sfx = PlayerPrefs.GetFloat("s.sfx", Sfx);
            Crowd = PlayerPrefs.GetFloat("s.crowd", Crowd);
            Music = PlayerPrefs.GetFloat("s.music", Music);
            Quality = PlayerPrefs.GetInt("s.quality", Quality);
            Fullscreen = PlayerPrefs.GetInt("s.fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            Fov = PlayerPrefs.GetFloat("s.fov", Fov);
            CamDistance = PlayerPrefs.GetFloat("s.camdist", CamDistance);
            MouseSens = PlayerPrefs.GetFloat("s.mouse", MouseSens);
            InvertY = PlayerPrefs.GetInt("s.invert", 0) == 1;
            AutoCamera = PlayerPrefs.GetInt("s.autocam", 1) == 1;
            Hints = PlayerPrefs.GetInt("s.hints", 1) == 1;
            Names = PlayerPrefs.GetInt("s.names", 1) == 1;
            ScreenShake = PlayerPrefs.GetInt("s.shake", 1) == 1;
            UiScale = PlayerPrefs.GetFloat("s.ui", UiScale);
            Brightness = PlayerPrefs.GetFloat("s.bright", Brightness);
            Post = PlayerPrefs.GetInt("s.post", 1) == 1;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat("s.master", Master);
            PlayerPrefs.SetFloat("s.sfx", Sfx);
            PlayerPrefs.SetFloat("s.crowd", Crowd);
            PlayerPrefs.SetFloat("s.music", Music);
            PlayerPrefs.SetInt("s.quality", Quality);
            PlayerPrefs.SetInt("s.fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetFloat("s.fov", Fov);
            PlayerPrefs.SetFloat("s.camdist", CamDistance);
            PlayerPrefs.SetFloat("s.mouse", MouseSens);
            PlayerPrefs.SetInt("s.invert", InvertY ? 1 : 0);
            PlayerPrefs.SetInt("s.autocam", AutoCamera ? 1 : 0);
            PlayerPrefs.SetInt("s.hints", Hints ? 1 : 0);
            PlayerPrefs.SetInt("s.names", Names ? 1 : 0);
            PlayerPrefs.SetInt("s.shake", ScreenShake ? 1 : 0);
            PlayerPrefs.SetFloat("s.ui", UiScale);
            PlayerPrefs.SetFloat("s.bright", Brightness);
            PlayerPrefs.SetInt("s.post", Post ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ResetDefaults()
        {
            Master = 0.8f; Sfx = 1f; Crowd = 0.8f; Music = 0.5f; Quality = 2; Fov = 62; CamDistance = 1; MouseSens = 1;
            InvertY = false; AutoCamera = true; Hints = true; Names = true; ScreenShake = true; UiScale = 1; Brightness = 0.9f; Post = true;
        }

        public static void ApplyDisplay()
        {
            if (Application.isEditor) return;
            if (Screen.fullScreen != Fullscreen)
                Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }
    }
}
