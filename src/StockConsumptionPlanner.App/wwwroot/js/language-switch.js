(function () {
    if (window.scpLanguageSwitchReady) {
        return;
    }

    window.scpLanguageSwitchReady = true;

    function languageSwitch() {
        return document.querySelector(".language-switch");
    }

    function toggleButton() {
        var host = languageSwitch();
        if (host === null) {
            return null;
        }

        return host.querySelector(".language-switch-toggle");
    }

    function menu() {
        var host = languageSwitch();
        if (host === null) {
            return null;
        }

        return host.querySelector(".language-switch-menu");
    }

    function options() {
        var list = menu();
        if (list === null) {
            return [];
        }

        return Array.prototype.slice.call(list.querySelectorAll("[role='option']"));
    }

    function isOpen() {
        var button = toggleButton();
        return button !== null && button.getAttribute("aria-expanded") === "true";
    }

    function elementFromEvent(event) {
        var target = event.target;
        if (target instanceof Text) {
            target = target.parentElement;
        }

        return target instanceof Element ? target : null;
    }

    function setOpen(open) {
        var button = toggleButton();
        var list = menu();
        if (button === null || list === null) {
            return;
        }

        button.setAttribute("aria-expanded", open ? "true" : "false");
        list.hidden = !open;
        if (!open) {
            return;
        }

        var current = list.querySelector("[aria-current='true']");
        var items = options();
        var target = current || items[0];
        if (target) {
            target.focus();
        }
    }

    function move(delta) {
        var items = options();
        if (items.length === 0) {
            return;
        }

        var index = items.indexOf(document.activeElement);
        if (index < 0) {
            index = delta > 0 ? -1 : 0;
        }

        var next = (index + delta + items.length) % items.length;
        items[next].focus();
    }

    document.addEventListener("click", function (event) {
        var target = elementFromEvent(event);
        if (target === null) {
            return;
        }

        if (target.closest(".language-switch-toggle") !== null) {
            setOpen(!isOpen());
            return;
        }

        if (!isOpen() || target.closest(".language-switch") !== null) {
            return;
        }

        setOpen(false);
    });

    document.addEventListener("keydown", function (event) {
        var target = elementFromEvent(event);
        if (target === null || target.closest(".language-switch") === null) {
            return;
        }

        var button = toggleButton();
        if (button === null) {
            return;
        }

        var onToggle = target.closest(".language-switch-toggle") !== null;
        var inMenu = target.closest(".language-switch-menu") !== null;
        if (!isOpen()) {
            if (!onToggle) {
                return;
            }

            if (event.key === "ArrowDown" ||
                event.key === "ArrowUp" ||
                event.key === "Enter" ||
                event.key === " ") {
                event.preventDefault();
                setOpen(true);
            }

            return;
        }

        if (event.key === "Escape") {
            event.preventDefault();
            setOpen(false);
            button.focus();
            return;
        }

        if (event.key === "Tab") {
            setOpen(false);
            return;
        }

        if (event.key === "ArrowDown") {
            event.preventDefault();
            move(1);
            return;
        }

        if (event.key === "ArrowUp") {
            event.preventDefault();
            move(-1);
            return;
        }

        if (event.key === " " && inMenu) {
            event.preventDefault();
            if (target instanceof HTMLAnchorElement) {
                target.click();
            }
        }
    });
})();
