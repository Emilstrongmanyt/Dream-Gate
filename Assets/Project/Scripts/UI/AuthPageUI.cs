using System;
using System.Collections;
using DreamGate.Battlegrounds.Services;
using DreamGate.Battlegrounds.Services.Backend;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamGate.Battlegrounds.UI
{
    public sealed class LoginPageView
    {
        private readonly GameObject root;
        private readonly TMP_InputField emailInput;
        private readonly TMP_InputField passwordInput;
        private readonly TextMeshProUGUI statusText;
        private readonly Action onSuccess;

        private LoginPageView(GameObject root, TMP_InputField emailInput, TMP_InputField passwordInput, TextMeshProUGUI statusText, Action onSuccess)
        {
            this.root = root;
            this.emailInput = emailInput;
            this.passwordInput = passwordInput;
            this.statusText = statusText;
            this.onSuccess = onSuccess;
        }

        public static LoginPageView Create(
            Transform parent,
            Action onBack,
            Action onSuccess,
            Action openCreateAccount)
        {
            var root = MenuPageUI.CreateOverlay(parent, "LoginPage");
            MenuPageUI.CreateTitle(root.transform, "Log In");
            MenuPageUI.CreateBody(
                root.transform,
                "LoginDescription",
                "Sign in with your email and password to play rated matches and sync progress.",
                620f,
                70f);

            var emailInput = MenuPageUI.CreateInputField(root.transform, "Email", "Email address", 430f);
            var passwordInput = MenuPageUI.CreateInputField(root.transform, "Password", "Password", 300f, true);
            var statusText = MenuPageUI.CreateStatusText(root.transform, 170f);

            var loginButton = MenuPageUI.CreateActionButton(root.transform, "Log In", new Vector2(0, 40), null);
            MenuPageUI.CreateActionButton(root.transform, "Create Account", new Vector2(0, -80), () => openCreateAccount?.Invoke());

            var view = new LoginPageView(root, emailInput, passwordInput, statusText, onSuccess);
            loginButton.onClick.AddListener(view.Submit);

            MenuPageUI.CreateBackButton(root.transform, () => onBack?.Invoke(), -760f);
            root.SetActive(false);
            return view;
        }

        public void Show()
        {
            statusText.text = string.Empty;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Hide() => root.SetActive(false);

        private void Submit()
        {
            if (DreamGateServices.UseCloudBackend)
            {
                statusText.color = Color.white;
                statusText.text = "Signing in...";
                CloudCoroutineHost.Instance.Run(SubmitRoutine());
                return;
            }

            if (DreamGateServices.TryLogin(emailInput.text, passwordInput.text, out var message))
            {
                statusText.color = new Color(0.55f, 0.95f, 0.65f);
                statusText.text = message;
                onSuccess?.Invoke();
                Hide();
                return;
            }

            statusText.color = new Color(1f, 0.55f, 0.55f);
            statusText.text = message;
        }

        private IEnumerator SubmitRoutine()
        {
            var success = false;
            var message = string.Empty;
            var finished = false;
            yield return AuthUiTimeout.Run(
                DreamGateServices.CoTryLogin(emailInput.text, passwordInput.text, (ok, msg) =>
                {
                    success = ok;
                    message = msg;
                    finished = true;
                }),
                () => finished,
                60f,
                "Sign in timed out. Check your connection and try again.",
                (timedOutMessage) =>
                {
                    message = timedOutMessage;
                });

            if (success)
            {
                statusText.color = new Color(0.55f, 0.95f, 0.65f);
                statusText.text = message;
                onSuccess?.Invoke();
                Hide();
                yield break;
            }

            statusText.color = new Color(1f, 0.55f, 0.55f);
            statusText.text = message;
        }
    }

    public sealed class CreateAccountPageView
    {
        private readonly GameObject root;
        private readonly TMP_InputField displayNameInput;
        private readonly TMP_InputField emailInput;
        private readonly TMP_InputField passwordInput;
        private readonly TMP_InputField confirmPasswordInput;
        private readonly TextMeshProUGUI statusText;
        private readonly Action onSuccess;

        private CreateAccountPageView(
            GameObject root,
            TMP_InputField displayNameInput,
            TMP_InputField emailInput,
            TMP_InputField passwordInput,
            TMP_InputField confirmPasswordInput,
            TextMeshProUGUI statusText,
            Action onSuccess)
        {
            this.root = root;
            this.displayNameInput = displayNameInput;
            this.emailInput = emailInput;
            this.passwordInput = passwordInput;
            this.confirmPasswordInput = confirmPasswordInput;
            this.statusText = statusText;
            this.onSuccess = onSuccess;
        }

        public static CreateAccountPageView Create(
            Transform parent,
            Action onBack,
            Action onSuccess,
            Action openLogin)
        {
            var root = MenuPageUI.CreateOverlay(parent, "CreateAccountPage");
            MenuPageUI.CreateTitle(root.transform, "Create Account");
            MenuPageUI.CreateBody(
                root.transform,
                "CreateDescription",
                "Create a Dream Gate account with your email and password.",
                660f,
                80f);

            var displayNameInput = MenuPageUI.CreateInputField(root.transform, "DisplayName", "Display name", 500f);
            var emailInput = MenuPageUI.CreateInputField(root.transform, "Email", "Email address", 380f);
            var passwordInput = MenuPageUI.CreateInputField(root.transform, "Password", "Password (6+ chars)", 260f, true);
            var confirmPasswordInput = MenuPageUI.CreateInputField(root.transform, "ConfirmPassword", "Confirm password", 140f, true);
            var statusText = MenuPageUI.CreateStatusText(root.transform, 10f);

            var createButton = MenuPageUI.CreateActionButton(root.transform, "Create Account", new Vector2(0, -110), null);
            MenuPageUI.CreateActionButton(root.transform, "Already have an account?", new Vector2(0, -210), () => openLogin?.Invoke());

            var view = new CreateAccountPageView(
                root,
                displayNameInput,
                emailInput,
                passwordInput,
                confirmPasswordInput,
                statusText,
                onSuccess);
            createButton.onClick.AddListener(view.Submit);

            MenuPageUI.CreateBackButton(root.transform, () => onBack?.Invoke(), -820f);
            root.SetActive(false);
            return view;
        }

        public void Show()
        {
            statusText.text = string.Empty;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Hide() => root.SetActive(false);

        private void Submit()
        {
            if (DreamGateServices.UseCloudBackend)
            {
                statusText.color = Color.white;
                statusText.text = "Creating account...";
                CloudCoroutineHost.Instance.Run(SubmitRoutine());
                return;
            }

            if (DreamGateServices.TryRegister(
                    displayNameInput.text,
                    emailInput.text,
                    passwordInput.text,
                    confirmPasswordInput.text,
                    out var message))
            {
                statusText.color = new Color(0.55f, 0.95f, 0.65f);
                statusText.text = message;
                onSuccess?.Invoke();
                Hide();
                return;
            }

            statusText.color = new Color(1f, 0.55f, 0.55f);
            statusText.text = message;
        }

        private IEnumerator SubmitRoutine()
        {
            var success = false;
            var message = string.Empty;
            var requiresEmailConfirmation = false;
            var finished = false;
            yield return AuthUiTimeout.Run(
                DreamGateServices.CoTryRegister(
                    displayNameInput.text,
                    emailInput.text,
                    passwordInput.text,
                    confirmPasswordInput.text,
                    (ok, msg, pendingConfirmation) =>
                    {
                        success = ok;
                        message = msg;
                        requiresEmailConfirmation = pendingConfirmation;
                        finished = true;
                    }),
                () => finished,
                55f,
                "Create account timed out. Check your connection and try again.",
                (timedOutMessage) =>
                {
                    message = timedOutMessage;
                });

            if (success)
            {
                statusText.color = new Color(0.55f, 0.95f, 0.65f);
                statusText.text = message;
                if (!requiresEmailConfirmation)
                {
                    onSuccess?.Invoke();
                    Hide();
                }

                yield break;
            }

            statusText.color = new Color(1f, 0.55f, 0.55f);
            statusText.text = message;
        }
    }

    internal static class AuthUiTimeout
    {
        public static IEnumerator Run(
            IEnumerator authRoutine,
            Func<bool> isFinished,
            float timeoutSeconds,
            string timeoutMessage,
            Action<string> setTimedOutMessage)
        {
            var deadline = AuthCoroutineTimeouts.CreateDeadline(timeoutSeconds);
            while (!isFinished() && !AuthCoroutineTimeouts.HasTimedOut(deadline))
            {
                if (authRoutine.MoveNext())
                {
                    yield return authRoutine.Current;
                }
                else
                {
                    break;
                }
            }

            if (!isFinished())
            {
                setTimedOutMessage(timeoutMessage);
            }
        }
    }
}