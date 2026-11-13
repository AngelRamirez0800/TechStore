// TechStore - JavaScript global

$(function () {

    $(document).on("submit", "form.swal-delete-form, form.swal-confirm-form", function (e) {
        e.preventDefault();

        const form = this;
        const $form = $(form);
        const isDelete = $form.hasClass("swal-delete-form");

        const title = $form.data("swal-title") || "¿Estás seguro?";
        const text = $form.data("swal-text") || (isDelete
            ? "El elemento será desactivado."
            : "¿Deseas confirmar esta acción?");
        const confirmText = $form.data("swal-confirm") || (isDelete
            ? "Sí, desactivar"
            : "Sí, confirmar");
        const confirmColor = $form.data("swal-color") || (isDelete ? "#dc3545" : "#0077b6");

        if (typeof Swal === "undefined") {
            form.submit();
            return;
        }

        Swal.fire({
            title: title,
            text: text,
            icon: "warning",
            showCancelButton: true,
            confirmButtonColor: confirmColor,
            cancelButtonColor: "#6c757d",
            confirmButtonText: confirmText,
            cancelButtonText: "Cancelar",
            reverseButtons: true
        }).then(function (result) {
            if (result.isConfirmed) {
                form.submit();
            }
        });
    });

    $(document).on("click", "[data-toggle-password]", function () {
        const targetSelector = $(this).data("toggle-password");
        const $input = targetSelector ? $(targetSelector) : $(this).closest(".input-group").find("input");
        if (!$input.length) return;

        const isPassword = $input.attr("type") === "password";
        $input.attr("type", isPassword ? "text" : "password");

        const $icon = $(this).find("i");
        if ($icon.length) {
            $icon.toggleClass("bi-eye bi-eye-slash");
        }
    });

});


(function () {
    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("[data-ts-multiselect-form]").forEach(function (form) {
            const checkboxes = form.querySelectorAll(".ts-multiselect-checkbox");
            const countBadge = form.querySelector("[data-ts-multiselect-count]");
            const clearButton = form.querySelector("[data-ts-multiselect-clear]");

            function updateCount() {
                if (!countBadge) {
                    return;
                }

                let checked = 0;
                checkboxes.forEach(function (box) {
                    if (box.checked) {
                        checked += 1;
                    }
                });

                countBadge.textContent = String(checked);
                countBadge.classList.toggle("d-none", checked === 0);
            }

            checkboxes.forEach(function (box) {
                box.addEventListener("change", updateCount);
            });

            if (clearButton) {
                clearButton.addEventListener("click", function () {
                    checkboxes.forEach(function (box) {
                        box.checked = false;
                    });
                    updateCount();
                });
            }

            updateCount();
        });

        document.querySelectorAll(".product-gallery").forEach(function (gallery) {
            const carousel = gallery.querySelector(".carousel");
            const thumbs = gallery.querySelectorAll(".product-gallery-thumb");
            if (!carousel || !thumbs.length) {
                return;
            }

            carousel.addEventListener("slide.bs.carousel", function (event) {
                thumbs.forEach(function (thumb, index) {
                    thumb.classList.toggle("border-primary", index === event.to);
                });
            });
        });
    });
})();
