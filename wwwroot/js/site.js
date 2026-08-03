(function () {
    "use strict";

    const translations = {
        en: {
            "nav.home": "Home", "nav.refresh": "Refresh Latest", "nav.registration": "App Registration", "theme.classic": "Classic", "theme.cloud": "Cloud",
            "identity.kicker": "Identity Demo", "identity.title": ".NET demo with MS Entra ID SSO",
            "identity.intro": "This sample supports two sign-in modes on one site: .NET/MSAL (app-managed) and App Service Easy Auth (platform-managed).",
            "mode.msal": "MSAL Mode", "mode.msal.description": "The ASP.NET Core app uses MSAL to initiate sign-in, process the authorization response, acquire tokens, and maintain the authenticated user session in application code.",
            "mode.easy": "Easy Auth Mode", "mode.easy.description": "Azure App Service authenticates the user before the request reaches ASP.NET Core, then provides verified identity details through platform-managed headers.",
            "signin.choose": "Choose an authentication mode to start sign-in.", "signin.msal": "Sign in with MSAL", "signin.easy": "Sign in with Easy Auth",
            "profile.kicker": "User Context", "profile.title": "Signed-in profile", "profile.authMode": "Authentication mode:",
            "profile.name": "Name", "profile.username": "Username", "profile.tenant": "Tenant", "profile.objectId": "Object ID", "profile.email": "Email", "profile.authType": "Authentication Type",
            "profile.displayName": "Display Name", "profile.principal": "Principal", "profile.claims": "All Available Claims", "profile.filter": "Filter claims by name or value",
            "action.copy": "Copy", "action.copied": "Copied", "action.signout": "Sign out", "action.backHome": "Back home",
            "panel.activeAuth": "Active auth:", "panel.notSignedIn": "Not signed in", "panel.description": "Identity mode validation and runtime status",
            "panel.localTime": "Local Time", "panel.date": "Date", "panel.autoSignout": "Auto Sign-out", "panel.authHealth": "Auth Health", "panel.ready": "Core auth configuration is ready.",
            "panel.attention": "At least one auth setting still needs attention.", "panel.timeline": "Session Timeline", "panel.noEvents": "No session events yet.",
            "error.title": "Authentication failed"
        },
        fr: {
            "nav.home": "Accueil", "nav.refresh": "Actualiser", "nav.registration": "Inscription d’application", "theme.classic": "Classique", "theme.cloud": "Nuage",
            "identity.kicker": "Démo d’identité", "identity.title": "Démo .NET avec l’authentification unique Microsoft Entra ID",
            "identity.intro": "Cet exemple propose deux modes de connexion sur un même site : .NET/MSAL (géré par l’application) et App Service Easy Auth (géré par la plateforme).",
            "mode.msal": "Mode MSAL", "mode.msal.description": "L’application ASP.NET Core utilise MSAL pour lancer la connexion, traiter la réponse d’autorisation, acquérir les jetons et maintenir la session authentifiée dans le code de l’application.",
            "mode.easy": "Mode Easy Auth", "mode.easy.description": "Azure App Service authentifie l’utilisateur avant que la requête n’atteigne ASP.NET Core, puis fournit les données d’identité vérifiées au moyen d’en-têtes gérés par la plateforme.",
            "signin.choose": "Choisissez un mode d’authentification pour vous connecter.", "signin.msal": "Se connecter avec MSAL", "signin.easy": "Se connecter avec Easy Auth",
            "profile.kicker": "Contexte utilisateur", "profile.title": "Profil connecté", "profile.authMode": "Mode d’authentification :",
            "profile.name": "Nom", "profile.username": "Nom d’utilisateur", "profile.tenant": "Locataire", "profile.objectId": "ID d’objet", "profile.email": "Courriel", "profile.authType": "Type d’authentification",
            "profile.displayName": "Nom d’affichage", "profile.principal": "Identité principale", "profile.claims": "Toutes les revendications disponibles", "profile.filter": "Filtrer par nom ou valeur",
            "action.copy": "Copier", "action.copied": "Copié", "action.signout": "Se déconnecter", "action.backHome": "Retour à l’accueil",
            "panel.activeAuth": "Authentification active :", "panel.notSignedIn": "Non connecté", "panel.description": "Validation du mode d’identité et état d’exécution",
            "panel.localTime": "Heure locale", "panel.date": "Date", "panel.autoSignout": "Déconnexion automatique", "panel.authHealth": "État de l’authentification", "panel.ready": "La configuration principale est prête.",
            "panel.attention": "Au moins un paramètre d’authentification nécessite votre attention.", "panel.timeline": "Chronologie de session", "panel.noEvents": "Aucun événement de session.",
            "error.title": "Échec de l’authentification"
        }
    };

    const root = document.documentElement;
    const themeSwitch = document.getElementById("themeSwitch");
    const languageOptions = Array.from(document.querySelectorAll("[data-language]"));
    let language = root.lang === "fr" ? "fr" : "en";

    function text(key) { return translations[language][key] || translations.en[key] || key; }
    function applyLanguage() {
        root.lang = language;
        document.querySelectorAll("[data-i18n]").forEach(function (el) { el.textContent = text(el.dataset.i18n); });
        document.querySelectorAll("[data-i18n-placeholder]").forEach(function (el) { el.placeholder = text(el.dataset.i18nPlaceholder); });
        languageOptions.forEach(function (button) {
            const active = button.dataset.language === language;
            button.classList.toggle("active", active);
            button.setAttribute("aria-pressed", String(active));
        });
        updateThemeButton();
    }
    function updateThemeButton() {
        if (!themeSwitch) return;
        const cloud = root.dataset.theme === "cloud";
        themeSwitch.setAttribute("aria-pressed", String(cloud));
        themeSwitch.querySelector(".theme-switch-icon").textContent = cloud ? "☼" : "◐";
        themeSwitch.querySelector(".theme-switch-label").textContent = text(cloud ? "theme.classic" : "theme.cloud");
        themeSwitch.setAttribute("aria-label", text(cloud ? "theme.classic" : "theme.cloud"));
    }
    if (themeSwitch) themeSwitch.addEventListener("click", function () {
        const next = root.dataset.theme === "cloud" ? "classic" : "cloud";
        root.dataset.theme = next;
        try { localStorage.setItem("ccoe-theme", next); } catch (_) { }
        updateThemeButton();
    });
    languageOptions.forEach(function (button) {
        button.addEventListener("click", function () {
            language = button.dataset.language === "fr" ? "fr" : "en";
            try { localStorage.setItem("ccoe-language", language); } catch (_) { }
            applyLanguage();
        });
    });
    applyLanguage();
    window.ccoeText = text;
})();
