// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function initReportTable() {
    const table = document.querySelector("[data-report-table]");
    if (!table) {
        return;
    }

    const searchInput = document.getElementById("reportSearch");
    const typeFilter = document.getElementById("reportTypeFilter");
    const sourceFilter = document.getElementById("reportSourceFilter");
    const pagination = document.getElementById("reportPagination");
    const rows = Array.from(table.querySelectorAll("tbody tr"));
    const pageSize = 8;
    let currentPage = 1;
    let sortDirection = 1;
    let sortIndex = 0;
    let sortType = "text";

    const applyFilters = () => {
        const query = searchInput?.value.toLowerCase() ?? "";
        const typeValue = typeFilter?.value ?? "";
        const sourceValue = sourceFilter?.value ?? "";

        return rows.filter((row) => {
            const matchesSearch = row.textContent.toLowerCase().includes(query);
            const matchesType = !typeValue || row.dataset.type === typeValue;
            const matchesSource = !sourceValue || row.dataset.source === sourceValue;
            return matchesSearch && matchesType && matchesSource;
        });
    };

    const sortRows = (list) => {
        return list.sort((a, b) => {
            const aCell = a.children[sortIndex]?.textContent.trim() ?? "";
            const bCell = b.children[sortIndex]?.textContent.trim() ?? "";
            if (sortType === "number") {
                const aNum = parseFloat(aCell.replace(/[^0-9.-]+/g, "")) || 0;
                const bNum = parseFloat(bCell.replace(/[^0-9.-]+/g, "")) || 0;
                return (aNum - bNum) * sortDirection;
            }
            if (sortType === "date") {
                const aDate = Date.parse(aCell) || 0;
                const bDate = Date.parse(bCell) || 0;
                return (aDate - bDate) * sortDirection;
            }
            return aCell.localeCompare(bCell) * sortDirection;
        });
    };

    const renderPage = () => {
        const filtered = sortRows(applyFilters());
        const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
        currentPage = Math.min(currentPage, totalPages);

        rows.forEach((row) => {
            row.style.display = "none";
        });

        const start = (currentPage - 1) * pageSize;
        const paged = filtered.slice(start, start + pageSize);
        paged.forEach((row) => {
            row.style.display = "";
        });

        if (pagination) {
            pagination.innerHTML = "";
            for (let i = 1; i <= totalPages; i += 1) {
                const button = document.createElement("button");
                button.type = "button";
                button.className = i === currentPage ? "page-btn active" : "page-btn";
                button.textContent = `${i}`;
                button.addEventListener("click", () => {
                    currentPage = i;
                    renderPage();
                });
                pagination.appendChild(button);
            }
        }
    };

    table.querySelectorAll("th[data-sort]").forEach((th, index) => {
        th.addEventListener("click", () => {
            sortDirection = sortIndex === index ? sortDirection * -1 : 1;
            sortIndex = index;
            sortType = th.dataset.sort || "text";
            renderPage();
        });
    });

    searchInput?.addEventListener("input", () => {
        currentPage = 1;
        renderPage();
    });
    typeFilter?.addEventListener("change", () => {
        currentPage = 1;
        renderPage();
    });
    sourceFilter?.addEventListener("change", () => {
        currentPage = 1;
        renderPage();
    });

    renderPage();
}

if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", () => {
        initReportTable();
    });
} else {
    initReportTable();
}
