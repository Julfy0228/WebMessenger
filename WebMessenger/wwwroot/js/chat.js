let currentChatId = null;
let currentChatType = 0;
let lastSenderId = null;
let myUserId = 0;
let pendingAttachments = [];

const roleNames = { 0: "Участник", 1: "Админ", 2: "Владелец", "Member": "Участник", "Admin": "Админ", "Owner": "Владелец" };

const connection = new signalR.HubConnectionBuilder().withUrl("/chathub").build();

const els = {
    chatsList: document.getElementById("chatsList"),
    placeholder: document.getElementById("selectChatPlaceholder"),
    chatContent: document.getElementById("chatContent"),
    chatTitle: document.getElementById("chatTitle"),
    chatAvatarHeader: document.getElementById("chatAvatarHeader"),
    chatHeaderStatus: document.getElementById("chatHeaderStatus"),
    chatHeaderClickable: document.getElementById("chatHeaderClickable"),
    messagesContainer: document.getElementById("messagesContainer"),
    messagesList: document.getElementById("messagesList"),
    messageInput: document.getElementById("messageInput"),
    sendForm: document.getElementById("sendForm"),
    participantsList: document.getElementById("participantsList"),
    createChatForm: document.getElementById("createChatForm"),
    addParticipantForm: document.getElementById("addParticipantForm"),
    profileContent: document.getElementById("profileContent"),
    currentUserBlock: document.getElementById("currentUserBlock"),
    myProfilePreview: document.getElementById("myProfilePreview"),
    updateDisplayNameForm: document.getElementById("updateDisplayNameForm"),
    updateUsernameForm: document.getElementById("updateUsernameForm"),
    updateEmailForm: document.getElementById("updateEmailForm"),
    changePasswordForm: document.getElementById("changePasswordForm"),
    findUserForm: document.getElementById("findUserForm"),
    searchResult: document.getElementById("searchResult"),
    searchError: document.getElementById("searchError"),
    dropZone: document.getElementById("dropZone"),
    dragOverlay: document.getElementById("dragOverlay"),
    attachmentsPreview: document.getElementById("attachmentsPreview"),
    fileInput: document.getElementById("fileInput"),

    btnAddMember: document.getElementById("btnAddMember"),
    btnDeleteChat: document.getElementById("btnDeleteChat")
};

function getAvatar(url, name) {
    if (url && url.length > 5) return url;
    const encodedName = encodeURIComponent(name || "U");
    return `https://ui-avatars.com/api/?name=${encodedName}&background=random&color=fff&size=128`;
}

fetch("/api/user/me").then(r => r.json()).then(u => {
    myUserId = u.id;
    renderMyProfile(u);
    connection.start().then(() => loadChats()).catch(err => console.error(err));
});

connection.on("ReceiveMessage", (message) => {
    if (message.chatId === currentChatId) {
        renderMessage(message);
        scrollToBottom();
    }
    updateLastMessageInList(message.chatId, message.text, message.sentAt);
});

connection.on("ChatCreated", () => loadChats());
connection.on("ParticipantAdded", (chatId) => { if (currentChatId === chatId) openChatInfo(chatId); loadChats(); });
connection.on("OwnershipTransferred", (chatId) => { if (currentChatId === chatId) openChatInfo(chatId); });
connection.on("RoleUpdated", (chatId) => { if (currentChatId === chatId) openChatInfo(chatId); });

connection.on("ChatDeleted", (chatId) => {
    if (currentChatId === chatId) {
        location.reload();
    } else {
        loadChats();
    }
});

connection.on("UserKicked", (chatId, userId) => {
    if (userId === myUserId) {
        if (currentChatId === chatId) location.reload();
        else loadChats();
    } else {
        if (currentChatId === chatId) openChatInfo(chatId);
        loadChats();
    }
});

els.sendForm.addEventListener("submit", (e) => { e.preventDefault(); sendMessage(); });
els.messageInput.addEventListener("keydown", (e) => { if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); sendMessage(); } });

els.createChatForm.addEventListener("submit", async e => {
    e.preventDefault();
    const name = document.getElementById("newChatName").value.trim() || "Новый чат";
    const res = await fetch("/api/chat", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name, type: 0, participantIds: [] }) });
    if (res.ok) { bootstrap.Modal.getInstance(document.getElementById("createChatModal")).hide(); loadChats(); document.getElementById("newChatName").value = ""; }
});

els.findUserForm.addEventListener("submit", async e => {
    e.preventDefault();
    const username = document.getElementById("searchUserNick").value;
    els.searchResult.classList.add("d-none"); els.searchError.classList.add("d-none");
    try {
        const res = await fetch(`/api/user?username=${username}`);
        if (res.ok) {
            const user = await res.json();
            document.getElementById("searchResAvatar").src = getAvatar(user.avatarUrl, user.displayName || user.userName);
            document.getElementById("searchResName").textContent = user.displayName || user.userName;
            document.getElementById("searchResNick").textContent = "@" + user.userName;
            els.searchResult.classList.remove("d-none"); els.searchResult.classList.add("d-flex");
            document.getElementById("btnStartPrivate").onclick = () => { createPrivateChat(user.id); bootstrap.Modal.getInstance(document.getElementById("createChatModal")).hide(); };
        } else els.searchError.classList.remove("d-none");
    } catch { els.searchError.classList.remove("d-none"); }
});

els.addParticipantForm.addEventListener("submit", async e => {
    e.preventDefault();
    const username = document.getElementById("addUserName").value;
    try {
        const userRes = await fetch(`/api/user?username=${username}`);
        if (!userRes.ok) throw new Error("Пользователь не найден");
        const user = await userRes.json();
        const res = await fetch(`/api/chat/${currentChatId}/participants`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ chatId: currentChatId, userId: user.id, role: 0 }) });
        if (res.ok) { bootstrap.Modal.getInstance(document.getElementById("addParticipantModal")).hide(); new bootstrap.Modal(document.getElementById('chatInfoModal')).show(); document.getElementById("addUserName").value = ""; }
        else alert("Ошибка добавления");
    } catch (ex) { alert(ex.message); }
});

els.chatHeaderClickable.addEventListener("click", () => {
    if (!currentChatId) return;

    if (currentChatType === 1) {
        const partnerId = els.chatAvatarHeader.dataset.partnerId;
        if (partnerId) openProfile(partnerId);
    } else {
        openChatInfo(currentChatId);
        new bootstrap.Modal(document.getElementById('chatInfoModal')).show();
    }
});

els.currentUserBlock.addEventListener("click", () => new bootstrap.Modal(document.getElementById('myProfileModal')).show());

async function handleUpdate(url, body) {
    const res = await fetch(url, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
    if (res.ok) { alert("Успешно!"); location.reload(); } else alert("Ошибка");
}
els.updateDisplayNameForm.addEventListener("submit", e => { e.preventDefault(); handleUpdate("/api/user/update-displayname", document.getElementById("editDisplayName").value); });
els.updateUsernameForm.addEventListener("submit", e => { e.preventDefault(); handleUpdate("/api/user/update-username", document.getElementById("editUsername").value); });
els.updateEmailForm.addEventListener("submit", e => { e.preventDefault(); handleUpdate("/api/user/update-email", document.getElementById("editEmail").value); });
els.changePasswordForm.addEventListener("submit", e => { e.preventDefault(); handleUpdate("/api/user/change-password", { currentPassword: document.getElementById("currPass").value, newPassword: document.getElementById("newPass").value }); });

els.dropZone.addEventListener("dragenter", e => { e.preventDefault(); if (currentChatId) els.dragOverlay.classList.remove("d-none"); els.dragOverlay.classList.add("d-flex"); });
els.dragOverlay.addEventListener("dragleave", e => { e.preventDefault(); els.dragOverlay.classList.add("d-none"); els.dragOverlay.classList.remove("d-flex"); });
els.dragOverlay.addEventListener("dragover", e => e.preventDefault());
els.dragOverlay.addEventListener("drop", e => { e.preventDefault(); els.dragOverlay.classList.add("d-none"); els.dragOverlay.classList.remove("d-flex"); if (e.dataTransfer.files.length > 0) handleFiles(e.dataTransfer.files); });
els.fileInput.addEventListener("change", e => { if (e.target.files.length > 0) handleFiles(e.target.files); });

function handleFiles(files) {
    els.attachmentsPreview.classList.remove("d-none");
    Array.from(files).forEach(file => {
        const reader = new FileReader();
        reader.onload = (e) => {
            let type = 0; if (file.type.startsWith("image/")) type = 1; else if (file.type.startsWith("audio/")) type = 2; else if (file.type.startsWith("video/")) type = 3;
            const att = { type, url: e.target.result, name: file.name, size: file.size };
            pendingAttachments.push(att); renderAttachmentPreview(att, pendingAttachments.length - 1);
        }; reader.readAsDataURL(file);
    });
}
function renderAttachmentPreview(att, i) {
    const div = document.createElement("div"); div.className = "position-relative border rounded overflow-hidden flex-shrink-0"; div.style.width = "60px"; div.style.height = "60px";
    let c = `<i class="bi bi-file-earmark fs-2 text-secondary d-block text-center mt-2"></i>`; if (att.type === 1) c = `<img src="${att.url}" class="w-100 h-100" style="object-fit:cover;">`;
    div.innerHTML = `${c}<button type="button" class="btn-close position-absolute top-0 end-0 bg-white p-1" style="width:8px;height:8px;" onclick="removeAttachment(${i},this)"></button>`;
    els.attachmentsPreview.appendChild(div);
}
window.removeAttachment = function (i, btn) { btn.parentElement.remove(); delete pendingAttachments[i]; if (els.attachmentsPreview.children.length === 0) { els.attachmentsPreview.classList.add("d-none"); pendingAttachments = []; } };

function renderMyProfile(u) {
    const avatar = getAvatar(u.avatarUrl, u.displayName || u.userName);
    els.currentUserBlock.innerHTML = `<img src="${avatar}" class="rounded-circle" width="38" height="38"><div class="overflow-hidden"><div class="fw-bold text-truncate" style="font-size:0.9rem;">${u.displayName || u.userName}</div><div class="small text-muted text-truncate" style="font-size:0.75rem;">#${u.userName}</div></div><div class="ms-auto text-secondary"><i class="bi bi-gear-fill"></i></div>`;
    els.myProfilePreview.innerHTML = `<img src="${avatar}" class="rounded-circle shadow-sm" width="80" height="80"><h5 class="mt-2">${u.displayName || u.userName}</h5><div class="text-muted small">@${u.userName}</div>`;
    document.getElementById("editDisplayName").value = u.displayName || ""; document.getElementById("editUsername").value = u.userName || ""; document.getElementById("editEmail").value = u.email || "";
}

function loadChats() {
    fetch("/api/chat/my").then(r => r.json()).then(chats => {
        els.chatsList.innerHTML = "";
        chats.forEach(chat => {
            const isActive = chat.id === currentChatId ? 'active' : '';
            let displayName = chat.name; let displayAvatar = getAvatar(null, chat.name);
            const count = chat.participants ? chat.participants.length : 1;
            if (chat.type === 1) {
                const other = chat.participants.find(p => p.userId !== myUserId);
                if (other) { displayName = other.displayName || other.userName; displayAvatar = getAvatar(null, displayName); }
            }
            const div = document.createElement("div"); div.className = `list-group-item chat-item py-3 ${isActive}`; div.id = `chat-item-${chat.id}`;
            div.onclick = () => selectChat(chat, div, displayName, displayAvatar, count);
            div.innerHTML = `<div class="d-flex align-items-center gap-3"><img src="${displayAvatar}" class="rounded-circle bg-white" width="48" height="48" style="object-fit:cover;"><div class="flex-grow-1 overflow-hidden"><div class="d-flex justify-content-between align-items-center"><span class="chat-name fw-bold text-truncate">${displayName}</span><small class="text-muted" style="font-size:0.75em">${chat.lastMessage ? new Date(chat.lastMessage.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : ''}</small></div><small class="text-muted text-truncate d-block" id="last-msg-${chat.id}">${chat.lastMessage ? chat.lastMessage.text : 'Нет сообщений'}</small></div></div>`;
            els.chatsList.appendChild(div);
        });
    });
}

function selectChat(chat, element, nameOverride, avatarOverride, count) {
    if (currentChatId === chat.id) return;
    currentChatId = chat.id;
    currentChatType = chat.type;

    document.querySelectorAll(".chat-item").forEach(i => i.classList.remove("active"));
    if (element) element.classList.add("active");
    els.placeholder.classList.add("d-none"); els.placeholder.classList.remove("d-flex");
    els.chatContent.classList.remove("d-none"); els.chatContent.classList.add("d-flex");

    els.chatTitle.textContent = nameOverride || chat.name;
    els.chatAvatarHeader.src = avatarOverride || getAvatar(null, chat.name);

    if (chat.type === 1) {
        const other = chat.participants.find(p => p.userId !== myUserId);
        if (other) els.chatAvatarHeader.dataset.partnerId = other.userId;
    }

    els.chatHeaderStatus.textContent = (chat.type === 1) ? "" : `${count} участников`;
    els.messagesList.innerHTML = ""; lastSenderId = null; els.messageInput.value = ""; els.messageInput.focus();
    pendingAttachments = []; els.attachmentsPreview.innerHTML = ""; els.attachmentsPreview.classList.add("d-none");

    connection.invoke("JoinChat", chat.id);
    fetch(`/api/chat/${chat.id}/messages`).then(r => r.json()).then(messages => { messages.forEach(renderMessage); scrollToBottom(); });
}

function renderMessage(m) {
    const isMine = m.senderId === myUserId;
    const sameSender = lastSenderId === m.senderId;
    lastSenderId = m.senderId;
    const wrapper = document.createElement("div"); wrapper.className = `d-flex gap-2 ${isMine ? "flex-row-reverse" : "flex-row"} ${sameSender ? "mt-1" : "mt-3"}`;
    const avatarVisibility = (!isMine && !sameSender) ? "visible" : "hidden";
    const avatarSrc = getAvatar(null, m.senderName);

    let attHtml = "";
    if (m.attachments && m.attachments.length > 0) {
        attHtml = `<div class="d-flex flex-wrap gap-2 mb-2">`;
        m.attachments.forEach(a => {
            if (a.type === 1) attHtml += `<a href="${a.url}" target="_blank"><img src="${a.url}" class="rounded border" style="max-width:200px;max-height:200px;object-fit:cover;"></a>`;
            else attHtml += `<a href="${a.url}" download="${a.name}" target="_blank" class="btn btn-sm btn-light border d-flex align-items-center gap-2 text-decoration-none text-dark" style="max-width:200px;"><i class="bi bi-file-earmark-arrow-down-fill text-primary"></i><div class="text-truncate">${a.name}</div></a>`;
        }); attHtml += `</div>`;
    }
    const nameHtml = (!isMine && !sameSender) ? `<div class="small text-muted ms-1 mb-1 profile-link" data-id="${m.senderId}">${m.senderName}</div>` : "";
    wrapper.innerHTML = `${isMine ? '' : `<img src="${avatarSrc}" class="rounded-circle profile-link" width="32" height="32" style="visibility:${avatarVisibility}" data-id="${m.senderId}">`}<div style="max-width:100%;display:flex;flex-direction:column;align-items:${isMine ? 'flex-end' : 'flex-start'}">${nameHtml}<div class="message-bubble ${isMine ? "message-mine" : "message-other"}">${attHtml}<div class="mb-1">${m.text || ""}</div><div class="text-end opacity-75" style="font-size:0.7em;margin-bottom:-4px;">${new Date(m.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</div></div></div>`;
    els.messagesList.appendChild(wrapper);
}

function sendMessage() {
    const text = els.messageInput.value.trim(); const valid = pendingAttachments.filter(a => a);
    if ((!text && valid.length === 0) || !currentChatId) return;
    els.messageInput.value = ""; pendingAttachments = []; els.attachmentsPreview.innerHTML = ""; els.attachmentsPreview.classList.add("d-none");
    fetch(`/api/chat/${currentChatId}/messages`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ text, attachments: valid }) }).catch(() => els.messageInput.value = text);
}
function updateLastMessageInList(chatId, t, time) { const el = document.getElementById(`last-msg-${chatId}`); if (el) el.textContent = t || "Вложение"; }
function scrollToBottom() { els.messagesContainer.scrollTop = els.messagesContainer.scrollHeight; }

function openChatInfo(chatId) {
    fetch(`/api/chat/${chatId}`).then(r => r.json()).then(chat => {
        els.participantsList.innerHTML = "";

        if (chat.type === 1) {
        }

        const myParticipant = chat.participants.find(p => p.userId === myUserId);
        const myRole = myParticipant ? (typeof myParticipant.role === 'string' ? myParticipant.role : roleNames[myParticipant.role]) : "Member";

        const isOwner = myRole === "Owner" || myRole === "Владелец" || myParticipant?.role === 2;
        const isAdmin = myRole === "Admin" || myRole === "Админ" || myParticipant?.role === 1;

        if (isOwner) els.btnDeleteChat.classList.remove("d-none");
        else els.btnDeleteChat.classList.add("d-none");

        if (chat.type === 1) els.btnAddMember.classList.add("d-none");
        else els.btnAddMember.classList.remove("d-none");

        chat.participants.forEach(p => {
            const isMe = p.userId === myUserId;
            const pRoleName = roleNames[p.role] || p.role;
            const pIsOwner = p.role === 2 || pRoleName === "Owner";
            const pIsAdmin = p.role === 1 || pRoleName === "Admin";

            let buttonsHtml = "";

            if (!isMe && chat.type === 0) {
                let canKick = false;
                if (isOwner) canKick = true;
                if (isAdmin && !pIsOwner && !pIsAdmin) canKick = true;

                let canPromote = false;
                if (isOwner && !pIsAdmin && !pIsOwner) canPromote = true;

                let canDemote = false;
                if (isOwner && pIsAdmin) canDemote = true;

                let canTransfer = isOwner;

                if (canTransfer) buttonsHtml += `<button class="btn btn-sm btn-outline-warning ms-1" title="Передать владение" onclick="transferOwnership(${p.userId})"><i class="bi bi-award"></i></button>`;

                if (canPromote) buttonsHtml += `<button class="btn btn-sm btn-outline-success ms-1" title="Сделать админом" onclick="promoteToAdmin(${p.userId})"><i class="bi bi-arrow-up-circle"></i></button>`;

                if (canDemote) buttonsHtml += `<button class="btn btn-sm btn-outline-secondary ms-1" title="Разжаловать до участника" onclick="demoteToMember(${p.userId})"><i class="bi bi-arrow-down-circle"></i></button>`;

                if (canKick) buttonsHtml += `<button class="btn btn-sm btn-outline-danger ms-1" title="Исключить" onclick="kickUser(${p.userId})"><i class="bi bi-x-lg"></i></button>`;
            }

            const li = document.createElement("li");
            li.className = "list-group-item d-flex align-items-center gap-3 px-0 border-0";
            li.innerHTML = `
                <img src="${getAvatar(null, p.displayName || p.userName)}" class="rounded-circle profile-link" width="36" height="36" data-id="${p.userId}">
                <div class="flex-grow-1">
                    <div class="fw-bold profile-link" data-id="${p.userId}">${p.displayName || p.userName}</div>
                    <small class="text-muted">@${p.userName}</small>
                </div>
                <span class="badge bg-light text-dark border">${roleNames[p.role] || p.role}</span>
                <div class="d-flex">${buttonsHtml}</div>
            `;
            els.participantsList.appendChild(li);
        });
    });
}

document.addEventListener("click", e => { const t = e.target.closest(".profile-link"); if (t) openProfile(t.dataset.id); });

function openProfile(id) {
    fetch(`/api/user/${id}`).then(r => r.json()).then(u => {
        const btn = (u.id !== myUserId) ? `<div class="d-grid"><button onclick="createPrivateChat('${u.id}')" class="btn btn-outline-primary btn-sm">Написать</button></div>` : '';
        els.profileContent.innerHTML = `<img src="${getAvatar(u.avatarUrl, u.displayName || u.userName)}" class="rounded-circle mb-3 shadow-sm" width="90" height="90"><h5 class="fw-bold">${u.displayName || u.userName}</h5><p class="text-muted mb-2">@${u.userName}</p><div class="badge ${u.isOnline ? 'bg-success' : 'bg-secondary'} mb-3">${u.isOnline ? 'Online' : 'Offline'}</div>${btn}`;
        new bootstrap.Modal(document.getElementById('profileModal')).show();
    });
}

window.createPrivateChat = async function (id) {
    const res = await fetch("/api/chat", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name: "P", type: 1, participantIds: [parseInt(id)] }) });
    if (res.ok) { const c = await res.json(); bootstrap.Modal.getOrCreateInstance(document.getElementById('profileModal')).hide(); await loadChats(); setTimeout(() => document.getElementById(`chat-item-${c.id}`)?.click(), 100); }
    else alert("Ошибка");
};

window.transferOwnership = async function (id) {
    if (!confirm("Передать права владельца?")) return;
    const res = await fetch(`/api/chat/${currentChatId}/transfer-ownership`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ newOwnerId: id }) });
    if (res.ok) openChatInfo(currentChatId); else alert("Ошибка");
};

window.deleteChat = async function () {
    if (!confirm("Удалить чат? Это действие нельзя отменить.")) return;
    const res = await fetch(`/api/chat/${currentChatId}`, { method: "DELETE" });
    if (res.ok) { bootstrap.Modal.getInstance(document.getElementById('chatInfoModal')).hide(); location.reload(); }
    else alert("Ошибка удаления");
};

window.promoteToAdmin = async function (id) {
    if (!confirm("Назначить администратором?")) return;
    const res = await fetch(`/api/chat/${currentChatId}/promote`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ userId: id }) });
    if (res.ok) openChatInfo(currentChatId); else alert("Ошибка");
};

window.demoteToMember = async function (id) {
    if (!confirm("Снять полномочия администратора?")) return;
    const res = await fetch(`/api/chat/${currentChatId}/demote`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ userId: id })
    });

    if (res.ok) {
        openChatInfo(currentChatId);
    } else {
        const err = await res.json();
        alert("Ошибка: " + (err.message || "Не удалось выполнить действие"));
    }
};

window.kickUser = async function (id) {
    if (!confirm("Исключить пользователя из чата?")) return;
    const res = await fetch(`/api/chat/${currentChatId}/kick`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ userId: id }) });
    if (res.ok) openChatInfo(currentChatId); else alert("Ошибка");
};

window.logout = function () { fetch("/api/user/logout", { method: "POST" }).then(() => window.location.href = "/"); };