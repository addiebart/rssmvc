const accountForm = document.querySelector("#account-form");
const accountEyebrow = document.querySelector("#account-eyebrow");
const accountHeading = document.querySelector("#account-heading");
const accountDescription = document.querySelector("#account-description");
const accountSubmit = document.querySelector("#account-submit");
const accountSwitchPrompt = document.querySelector("#account-switch-prompt");
const accountSwitch = document.querySelector("#account-switch");
const usernameInput = document.querySelector("#username");
const passwordInput = document.querySelector("#password");
const isRegisteringInput = document.querySelector("#is-registering");
let isRegistering = isRegisteringInput.value.toLowerCase() === "true";

accountForm.addEventListener("submit", (event) => {
    isRegisteringInput.value = isRegistering;
});

const updateAccountMode = () => {
    accountEyebrow.textContent = isRegistering ? "Get started" : "Welcome back";
    accountHeading.textContent = isRegistering ? "Create your RssAsp account" : "Log in to RssAsp";
    accountDescription.textContent = isRegistering
        ? "Create an account to start following your favorite feeds."
        : "Read everything you follow in one place.";
    accountSubmit.textContent = isRegistering ? "Register" : "Log in";
    accountSwitchPrompt.textContent = isRegistering ? "Already have an account?" : "Need an account?";
    accountSwitch.textContent = isRegistering ? "Log in" : "Register";
    passwordInput.autocomplete = isRegistering ? "new-password" : "current-password";
    isRegisteringInput.value = isRegistering;
};

accountSwitch.addEventListener("click", () => {
    isRegistering = !isRegistering;
    updateAccountMode();
    usernameInput.focus();
});

updateAccountMode();
