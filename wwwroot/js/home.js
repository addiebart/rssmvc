const addFeedButton = document.querySelector("#add-feed-button");
const addFeedModal = document.querySelector("#add-feed-modal");
const closeAddFeedModalButton = document.querySelector("#close-add-feed-modal");
const cancelAddFeedButton = document.querySelector("#cancel-add-feed");
const addFeedForm = addFeedModal.querySelector("form");
const addFeedError = document.createElement("p");
const deleteFeedModal = document.querySelector("#delete-feed-modal");
const deleteFeedForm = document.querySelector("#delete-feed-form");
const deleteFeedName = document.querySelector("#delete-feed-name");
const deleteFeedError = document.createElement("p");
const closeDeleteFeedModalButton = document.querySelector("#close-delete-feed-modal");
const cancelDeleteFeedButton = document.querySelector("#cancel-delete-feed");
const removeFeedButtons = document.querySelectorAll(".remove-feed-button");
const feedListDetails = document.querySelector("#feed-list-details");
const sortOrder = document.querySelector("#sort-order");
const viewModes = document.querySelectorAll("input[name='view']");
const feedSelectionButtons = document.querySelectorAll(".feed-selection-button");
let selectedFeedItem;

const mediumScreenQuery = window.matchMedia("(min-width: 48rem)");
feedListDetails.open = mediumScreenQuery.matches;
mediumScreenQuery.addEventListener("change", (event) => {
    if (event.matches) {
        feedListDetails.open = true;
    }
});

addFeedButton.addEventListener("click", (event) => {
    event.stopPropagation();
    event.preventDefault();
});

addFeedError.className = "mt-4 text-sm text-red-600 dark:text-red-400";
addFeedError.setAttribute("role", "alert");
addFeedError.hidden = true;
addFeedForm.append(addFeedError);
deleteFeedError.className = "mb-4 text-sm text-red-600 dark:text-red-400";
deleteFeedError.setAttribute("role", "alert");
deleteFeedError.hidden = true;
deleteFeedForm.append(deleteFeedError);

const setPreferenceCookie = (name, value) => {
    document.cookie = `${name}=${encodeURIComponent(value)}; max-age=31536000; path=/; samesite=lax`;
};

const showModalThrobber = (form, label) => {
    const throbber = document.createElement("div");
    throbber.className = "flex min-h-40 items-center justify-center p-6";
    throbber.setAttribute("role", "status");
    throbber.setAttribute("aria-label", label);
    throbber.innerHTML = `
        <svg class="h-8 w-8 text-gray-500 dark:text-gray-400" viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <g>
                <circle opacity=".25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                <path opacity=".75" fill="currentColor" d="M4 12a8 8 0 0 1 8-8V0C5.373 0 0 5.373 0 12h4Z"></path>
                <animateTransform attributeName="transform" type="rotate" from="0 12 12" to="360 12 12" dur=".8s" repeatCount="indefinite"></animateTransform>
            </g>
        </svg>
    `;
    form.replaceWith(throbber);
    return throbber;
};

const reloadHome = () => {
    const url = new URL(window.location.href);
    url.searchParams.delete("sort");
    url.searchParams.delete("direction");
    url.searchParams.delete("view");
    window.location.href = url.toString();
};

const selectFeed = (button) => {
    const radio = button.querySelector("input[name='feed-selection']");

    feedSelectionButtons.forEach((feedButton) => {
        const feedRadio = feedButton.querySelector("input[name='feed-selection']");
        const isSelected = feedButton === button;

        feedRadio.checked = isSelected;
        feedButton.setAttribute("aria-checked", isSelected.toString());
        feedButton.classList.toggle("bg-slate-100", isSelected);
        feedButton.classList.toggle("text-gray-900", isSelected);
        feedButton.classList.toggle("text-gray-600", !isSelected);
        feedButton.classList.toggle("dark:bg-slate-800", isSelected);
        feedButton.classList.toggle("dark:text-gray-100", isSelected);
        feedButton.classList.toggle("dark:text-gray-300", !isSelected);
    });

    const url = new URL(window.location.href);
    if (radio.value === "All posts") {
        url.searchParams.delete("subscriptionId");
    } else {
        url.searchParams.set("subscriptionId", radio.value);
    }

    window.location.href = url.toString();
};

feedSelectionButtons.forEach((button) => {
    button.addEventListener("click", (event) => {
        if (event.target.closest(".remove-feed-button")) {
            return;
        }

        selectFeed(button);
    });

    button.addEventListener("keydown", (event) => {
        if (event.key === " " || event.key === "Enter") {
            event.preventDefault();
            selectFeed(button);
        }
    });
});

sortOrder.addEventListener("change", () => {
    const [criteria, direction] = sortOrder.value.split("-");
    setPreferenceCookie("rssasp.sort", criteria);
    setPreferenceCookie("rssasp.direction", direction);
    reloadHome();
});

viewModes.forEach((viewMode) => {
    viewMode.addEventListener("change", () => {
        setPreferenceCookie("rssasp.view", viewMode.value);
        reloadHome();
    });
});

const closeAddFeedModal = () => {
    addFeedForm.reset();
    addFeedError.textContent = "";
    addFeedError.hidden = true;

    if (addFeedModal.open) {
        addFeedModal.close();
    }
};

addFeedButton.addEventListener("click", () => {
    addFeedModal.showModal();
});

closeAddFeedModalButton.addEventListener("click", () => {
    closeAddFeedModal();
});

cancelAddFeedButton.addEventListener("click", () => {
    closeAddFeedModal();
});

addFeedModal.addEventListener("click", (event) => {
    if (event.target === addFeedModal) {
        closeAddFeedModal();
    }
});

addFeedModal.addEventListener("close", closeAddFeedModal);

addFeedForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    addFeedError.textContent = "";
    addFeedError.hidden = true;
    const throbber = showModalThrobber(addFeedForm, "Adding feed");

    try {
        const response = await fetch(addFeedForm.action, {
            method: addFeedForm.method || "POST",
            body: new FormData(addFeedForm),
            headers: {
                Accept: "application/json"
            }
        });

        if (response.ok) {
            window.location.assign(response.url);
            return;
        }

        let message = "The feed could not be added.";
        try {
            const result = await response.json();
            const errors = Object.values(result)
                .flat()
                .filter((error) => typeof error === "string");
            if (errors.length > 0) {
                message = errors.join(" ");
            }
        } catch {
            // Use the generic message when the server response is not JSON.
        }

        addFeedError.textContent = message;
        addFeedError.hidden = false;
        throbber.replaceWith(addFeedForm);
    } catch {
        addFeedError.textContent = "The feed could not be added. Please try again.";
        addFeedError.hidden = false;
        throbber.replaceWith(addFeedForm);
    }
});

const closeDeleteFeedModal = () => {
    deleteFeedForm.reset();
    deleteFeedName.textContent = "";
    deleteFeedError.textContent = "";
    deleteFeedError.hidden = true;

    if (deleteFeedModal.open) {
        deleteFeedModal.close();
    }
};

removeFeedButtons.forEach((button) => {
    button.addEventListener("click", () => {
        const subscriptionId = button.dataset.subscriptionId;
        deleteFeedForm.action = `/Subscription/Delete/${subscriptionId}`;
        deleteFeedName.textContent = button.dataset.subscriptionTitle;
        selectedFeedItem = button.closest(".feed-item");
        deleteFeedModal.showModal();
    });
});

closeDeleteFeedModalButton.addEventListener("click", closeDeleteFeedModal);
cancelDeleteFeedButton.addEventListener("click", closeDeleteFeedModal);

deleteFeedModal.addEventListener("click", (event) => {
    if (event.target === deleteFeedModal) {
        closeDeleteFeedModal();
    }
});

deleteFeedForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    deleteFeedError.textContent = "";
    deleteFeedError.hidden = true;
    const throbber = showModalThrobber(deleteFeedForm, "Removing feed");

    try {
        const response = await fetch(deleteFeedForm.action, {
            method: deleteFeedForm.method || "POST",
            body: new FormData(deleteFeedForm),
            headers: {
                Accept: "application/json"
            }
        });

        if (response.ok) {
            window.location.assign(response.url);
            return;
        }

        deleteFeedError.textContent = "The feed could not be removed.";
        deleteFeedError.hidden = false;
        throbber.replaceWith(deleteFeedForm);
    } catch {
        deleteFeedError.textContent = "The feed could not be removed. Please try again.";
        deleteFeedError.hidden = false;
        throbber.replaceWith(deleteFeedForm);
    }
});

deleteFeedModal.addEventListener("close", closeDeleteFeedModal);
