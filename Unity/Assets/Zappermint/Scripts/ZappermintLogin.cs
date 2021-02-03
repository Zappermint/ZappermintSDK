﻿using System;
using System.Collections;
using System.Web;
using UnityEngine;

namespace Zappermint
{
    public class ZappermintLogin : MonoBehaviour
    {
        [Tooltip("App name. Will be displayed on the Zapp Wallet login page.")]
        public string AppName = "My App";
        [Tooltip("App icon. Will be displayed on the Zapp Wallet login page. Optimally, 192x192px. Texture needs to be readable")]
        public Texture2D AppIcon;
        [Tooltip("Deeplink scheme. Must match AndroidManifest and/or Project Settings")]
        public string Scheme = "myapp";
        [Tooltip("Amount of ZAPP user must stake to use app ad-free")]
        public int Cost = 10;

        [Tooltip("Event fired when login completes")]
        public LoginEvent OnLogin = new LoginEvent();
        [Tooltip("Event fired when login completes successfully")]
        public LoginSuccessEvent OnSuccess = new LoginSuccessEvent();
        [Tooltip("Event fired when login completes unsuccessfully")]
        public LoginFailEvent OnFail = new LoginFailEvent();

        private string _icon64;
        private Coroutine _loginFallback;

        private void OnEnable()
        {
            ZappermintLinkManager.Instance.Handler += LinkHandler;
        }

        private void OnDisable()
        {
            ZappermintLinkManager.Instance.Handler -= LinkHandler;

        }

        private void Start()
        {
            if (string.IsNullOrEmpty(AppName))
            {
                Debug.LogWarning("Please fill in the App Name in the ZappermintLogin script");
            }
            else
            {
                AppName = HttpUtility.UrlEncode(AppName);
                //AppName = Regex.Replace(AppName, @"[^a-zA-Z0-9 -_]", "_");
            }

            if (AppIcon == null)
            {
                Debug.LogWarning("Please fill in the App Icon in the ZappermintLogin script");
            }
            else
            {
#if UNITY_EDITOR
                if (!AppIcon.isReadable)
                {
                    var textureImporter = UnityEditor.AssetImporter.GetAtPath(UnityEditor.AssetDatabase.GetAssetPath(AppIcon)) as UnityEditor.TextureImporter;
                    textureImporter.isReadable = true;
                    textureImporter.SaveAndReimport();
                }
#endif
                var copy = new Texture2D(AppIcon.width, AppIcon.height, TextureFormat.RGB24, false);
                copy.name = $"{AppIcon.name}_192x192";
                var pixels = AppIcon.GetPixels32();
                copy.SetPixels32(pixels);
                copy.Apply();
                TextureScale.Bilinear(copy, 192, 192);
                AppIcon = copy;
                var bytes = copy.EncodeToJPG();
                _icon64 = Convert.ToBase64String(bytes);
            }

            Debug.Log($"zappermint://login?c={Cost}&r={Scheme}&n={AppName}&i={_icon64}");
        }

        /// <summary>
        /// Logs in through the Zapp Wallet
        /// </summary>
        public void Login()
        {
            Application.OpenURL($"zappermint://login?c={Cost}&r={Scheme}&n={AppName}&i={_icon64}");
            _loginFallback = StartCoroutine(OpenStore());
        }

        private IEnumerator OpenStore()
        {
            yield return new WaitForSeconds(2f);
#if UNITY_EDITOR || UNITY_ANDROID
            Application.OpenURL($"https://play.google.com/store/apps/details?id=com.ZappermintBV.ZappWallet");
#elif UNITY_IOS
            Application.OpenURL($"https://itunes.apple.com/us/app/zappermint/id1234");
#endif
            _loginFallback = null;
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && _loginFallback != null)
            {
                StopCoroutine(_loginFallback);
                _loginFallback = null;
            }
        }

        /// <summary>
        /// Handles the Deeplink for logging in
        /// </summary>
        /// <param name="link"></param>
        private void LinkHandler(DeepLink link)
        {
            ZappermintLinkManager manager = ZappermintLinkManager.Instance;
            LoginResult result;
            switch (link.host)
            {
                case "zappermint.login.success":
                    result = new LoginResult(new LoginData(manager.GetParameter("w"), manager.GetParameter("n")));
                    manager.Account = new Account(result.data);
                    OnSuccess.Invoke(result.data);
                    break;
                case "zappermint.login.fail":
                    result = new LoginResult(new LoginError(manager.GetParameter("e")));
                    OnFail.Invoke(result.error);
                    break;
                default:
                    result = default;
                    Debug.LogWarning($"Zappermint Deep Link unrecognized host '{link.host}'.");
                    break;
            }
            OnLogin.Invoke(result);
        }
    }
}