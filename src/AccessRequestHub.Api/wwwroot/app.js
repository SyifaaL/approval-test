// Vanilla JS, no build step. Hash-based routing: #/new, #/mine, #/inbox, #/requests/:id.
// The user switcher (localStorage email -> X-User-Email header) simulates authentication only;
// it is not a security boundary. The backend enforces every authorization rule server-side.

const APPLICATIONS = ["CRM", "Finance Portal"];
const ENVIRONMENTS = ["NonProduction", "Production"];
const ACCESS_LEVELS = ["Read", "Admin"];
const CURRENT_USER_KEY = "arh_current_user";

const app = document.getElementById("app");
const userSelect = document.getElementById("user-select");

function getCurrentUserEmail() {
  return localStorage.getItem(CURRENT_USER_KEY) || "";
}

function setCurrentUserEmail(email) {
  localStorage.setItem(CURRENT_USER_KEY, email);
}

async function api(path, options = {}) {
  const headers = Object.assign({ "Content-Type": "application/json" }, options.headers || {});
  const email = getCurrentUserEmail();
  if (email) headers["X-User-Email"] = email;

  const res = await fetch(path, { ...options, headers });
  const text = await res.text();
  let body = null;
  if (text) {
    try { body = JSON.parse(text); } catch { body = text; }
  }

  if (!res.ok) {
    const message = (body && (body.detail || body.title)) || res.statusText || "Request failed";
    const error = new Error(message);
    error.status = res.status;
    error.code = body && body.code;
    throw error;
  }

  return body;
}

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value ?? "";
  return div.innerHTML;
}

function formatDate(iso) {
  try { return new Date(iso).toLocaleString(); } catch { return iso; }
}

function renderLoading() {
  app.innerHTML = `<p class="muted">Loading...</p>`;
}

function renderErrorBanner(error) {
  const code = error.code ? ` (${escapeHtml(error.code)})` : "";
  return `<div class="banner error">Error${code}: ${escapeHtml(error.message)}</div>`;
}

// ---------- User switcher ----------

async function initUserSwitcher() {
  const users = await api("/api/users");
  userSelect.innerHTML = users.map(u => `<option value="${escapeHtml(u.email)}">${escapeHtml(u.name)} (${escapeHtml(u.email)})</option>`).join("");

  const current = getCurrentUserEmail();
  if (current && users.some(u => u.email === current)) {
    userSelect.value = current;
  } else if (users.length > 0) {
    setCurrentUserEmail(users[0].email);
    userSelect.value = users[0].email;
  }

  userSelect.addEventListener("change", () => {
    setCurrentUserEmail(userSelect.value);
    router();
  });
}

// ---------- Router ----------

function highlightNav(route) {
  document.querySelectorAll("#nav a").forEach(a => {
    a.classList.toggle("active", a.dataset.route === route);
  });
}

function parseHash() {
  const hash = location.hash.slice(1) || "/new";
  const detailMatch = hash.match(/^\/requests\/([0-9a-fA-F-]{36})$/);
  if (detailMatch) return { view: "detail", id: detailMatch[1] };
  return { view: hash };
}

async function router() {
  const { view, id } = parseHash();
  highlightNav(view === "detail" ? null : view);
  try {
    if (view === "detail") await renderRequestDetail(id);
    else if (view === "/mine") await renderMyRequests();
    else if (view === "/inbox") await renderInbox();
    else await renderNewRequestForm();
  } catch (error) {
    app.innerHTML = renderErrorBanner(error);
  }
}

// ---------- New Request ----------

let pendingClientRequestId = null;

async function renderNewRequestForm() {
  // Generated once per time the form is (re)opened, not per click, so a retry of the same
  // in-flight submission reuses the same ClientRequestId instead of creating a new logical request.
  pendingClientRequestId = crypto.randomUUID();

  app.innerHTML = `
    <div class="card">
      <h2>New Access Request</h2>
      <form id="new-request-form">
        <div class="field">
          <label for="applicationName">Application</label>
          <select id="applicationName" name="applicationName">
            ${APPLICATIONS.map(a => `<option value="${a}">${a}</option>`).join("")}
          </select>
        </div>
        <div class="field">
          <label for="environment">Environment</label>
          <select id="environment" name="environment">
            ${ENVIRONMENTS.map(e => `<option value="${e}">${e}</option>`).join("")}
          </select>
        </div>
        <div class="field">
          <label for="accessLevel">Access level</label>
          <select id="accessLevel" name="accessLevel">
            ${ACCESS_LEVELS.map(l => `<option value="${l}">${l}</option>`).join("")}
          </select>
        </div>
        <div class="field">
          <label for="justification">Justification</label>
          <textarea id="justification" name="justification" required></textarea>
        </div>
        <div id="form-message"></div>
        <button type="submit">Submit request</button>
      </form>
    </div>
  `;

  const form = document.getElementById("new-request-form");
  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    const submitButton = form.querySelector("button[type=submit]");
    const messageBox = document.getElementById("form-message");
    submitButton.disabled = true;
    messageBox.innerHTML = "";

    try {
      const payload = {
        clientRequestId: pendingClientRequestId,
        applicationName: form.applicationName.value,
        environment: form.environment.value,
        accessLevel: form.accessLevel.value,
        justification: form.justification.value,
      };
      const created = await api("/api/requests", { method: "POST", body: JSON.stringify(payload) });
      location.hash = `#/requests/${created.id}`;
    } catch (error) {
      messageBox.innerHTML = renderErrorBanner(error);
      submitButton.disabled = false;
    }
  });
}

// ---------- My Requests ----------

function statusBadge(status) {
  return `<span class="badge ${status}">${status}</span>`;
}

function renderRequestRows(requests, { showRequester } = {}) {
  if (requests.length === 0) {
    return `<div class="banner empty">No requests to show.</div>`;
  }
  return `
    <table>
      <thead>
        <tr>
          ${showRequester ? "<th>Requester</th>" : ""}
          <th>Application</th><th>Environment</th><th>Access level</th><th>Status</th><th>Created</th>
        </tr>
      </thead>
      <tbody>
        ${requests.map(r => `
          <tr class="clickable" data-id="${r.id}">
            ${showRequester ? `<td>${escapeHtml(r.requesterEmail)}</td>` : ""}
            <td>${escapeHtml(r.applicationName)}</td>
            <td>${escapeHtml(r.environment)}</td>
            <td>${escapeHtml(r.accessLevel)}</td>
            <td>${statusBadge(r.status)}</td>
            <td>${formatDate(r.createdAt)}</td>
          </tr>
        `).join("")}
      </tbody>
    </table>
  `;
}

function wireRowClicks() {
  app.querySelectorAll("tr.clickable").forEach(row => {
    row.addEventListener("click", () => {
      location.hash = `#/requests/${row.dataset.id}`;
    });
  });
}

async function renderMyRequests() {
  renderLoading();
  const requests = await api("/api/requests/mine");
  app.innerHTML = `<h2>My Requests</h2>${renderRequestRows(requests)}`;
  wireRowClicks();
}

async function renderInbox() {
  renderLoading();
  const requests = await api("/api/inbox");
  app.innerHTML = `<h2>Approval Inbox</h2>${renderRequestRows(requests, { showRequester: true })}`;
  wireRowClicks();
}

// ---------- Request detail ----------

async function renderRequestDetail(id, bannerHtml = "") {
  renderLoading();
  let detail;
  try {
    detail = await api(`/api/requests/${id}`);
  } catch (error) {
    app.innerHTML = renderErrorBanner(error);
    return;
  }

  const isPending = detail.status === "PendingManager" || detail.status === "PendingSystemOwner";

  app.innerHTML = `
    ${bannerHtml}
    <div class="card">
      <h2>${escapeHtml(detail.applicationName)} &mdash; ${statusBadge(detail.status)}</h2>
      <p><strong>Requester:</strong> ${escapeHtml(detail.requesterEmail)}</p>
      <p><strong>Environment:</strong> ${escapeHtml(detail.environment)} &nbsp;
         <strong>Access level:</strong> ${escapeHtml(detail.accessLevel)} &nbsp;
         <strong>High risk:</strong> ${detail.isHighRisk ? "Yes" : "No"}</p>
      <p><strong>Justification:</strong> ${escapeHtml(detail.justification)}</p>
      <p><strong>Policy version:</strong> ${escapeHtml(detail.policyVersion)} &nbsp;
         <strong>Version:</strong> ${detail.version}</p>
      ${detail.rejectionReason ? `<p><strong>Rejection reason:</strong> ${escapeHtml(detail.rejectionReason)}</p>` : ""}
    </div>

    ${isPending ? `
      <div class="card">
        <h3>Decision</h3>
        <p class="muted">
          Buttons are shown regardless of who is viewing &mdash; the backend is the real
          authorization boundary, not this UI (see assessment requirement).
        </p>
        <div id="decision-message"></div>
        <div class="field">
          <label for="reject-reason">Reason (required to reject)</label>
          <textarea id="reject-reason"></textarea>
        </div>
        <button id="approve-btn">Approve</button>
        <button id="reject-btn" class="danger">Reject</button>
      </div>
    ` : ""}

    <div class="card">
      <h3>Audit timeline</h3>
      <ul class="timeline">
        ${detail.auditTrail.map(e => `
          <li>
            <strong>${escapeHtml(e.eventType)}</strong>
            by ${escapeHtml(e.actorEmail)} on ${formatDate(e.occurredAt)}
            ${e.fromStatus ? ` &mdash; ${escapeHtml(e.fromStatus)} &rarr; ${escapeHtml(e.toStatus)}` : ` &mdash; ${escapeHtml(e.toStatus)}`}
            ${e.reason ? `<br><span class="muted">Reason: ${escapeHtml(e.reason)}</span>` : ""}
          </li>
        `).join("")}
      </ul>
    </div>
  `;

  if (!isPending) return;

  const approveBtn = document.getElementById("approve-btn");
  const rejectBtn = document.getElementById("reject-btn");
  const messageBox = document.getElementById("decision-message");

  async function decide(action) {
    approveBtn.disabled = true;
    rejectBtn.disabled = true;
    messageBox.innerHTML = "";

    const reason = document.getElementById("reject-reason").value;
    try {
      await api(`/api/requests/${id}/${action}`, {
        method: "POST",
        body: JSON.stringify({ expectedVersion: detail.version, reason: action === "reject" ? reason : null }),
      });
      await renderRequestDetail(id);
    } catch (error) {
      if (error.status === 409) {
        await renderRequestDetail(id, `<div class="banner conflict">Request sudah berubah sejak kamu membuka halaman ini. Data di bawah sudah direfresh &mdash; coba lagi.</div>`);
      } else {
        messageBox.innerHTML = renderErrorBanner(error);
        approveBtn.disabled = false;
        rejectBtn.disabled = false;
      }
    }
  }

  approveBtn.addEventListener("click", () => decide("approve"));
  rejectBtn.addEventListener("click", () => decide("reject"));
}

// ---------- Boot ----------

async function init() {
  try {
    await initUserSwitcher();
  } catch (error) {
    app.innerHTML = renderErrorBanner(error);
    return;
  }
  await router();
}

window.addEventListener("hashchange", router);
window.addEventListener("DOMContentLoaded", init);
