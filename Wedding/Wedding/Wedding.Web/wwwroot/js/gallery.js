document.querySelectorAll(".gallery-item").forEach(item => {
    item.addEventListener("click", () => {
        const url = item.dataset.url;
        const type = item.dataset.type;
        const container = document.getElementById("modalContent");

        container.innerHTML = "";

        if (type === "photo") {
            container.innerHTML =
                `<img src="${url}" class="img-fluid rounded" />`;
        } else {
            container.innerHTML =
                `<video src="${url}" controls autoplay class="w-100 rounded"></video>`;
        }

        document.getElementById("downloadBtn").href = url;

        new bootstrap.Modal(
            document.getElementById("mediaModal")
        ).show();
    });
});
