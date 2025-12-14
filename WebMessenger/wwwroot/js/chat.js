let currentChatId = null;
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
    myProfileModalContent: document.getElementById("myProfileModalContent"),

    dropZone: document.getElementById("dropZone"),
    dragOverlay: document.getElementById("dragOverlay"),
    attachmentsPreview: document.getElementById("attachmentsPreview"),
    fileInput: document.getElementById("fileInput")
};

function getAvatar(url, name) {
    if (url && url.length > 5) return url;
    const encodedName = encodeURIComponent(name || "U");
    return `https://ui-avatars.com/api/?name=${encodedName}&background=random&color=fff&size=128`;
}

fetch("/api/user/me").then(r => r.json()).then(u => {
    myUserId = u.id;
    renderMyProfile(u);

    connection.start().then(() => {
        loadChats();
    }).catch(err => console.error(err));
});

connection.on("ReceiveMessage", (message) => {
    if (message.chatId === currentChatId) {
        renderMessage(message);
        scrollToBottom();
    }
    updateLastMessageInList(message.chatId, message.text, message.sentAt);
});

connection.on("ChatCreated", () => loadChats());
connection.on("ParticipantAdded", (chatId) => {
    if (currentChatId === chatId) {
        openChatInfo(chatId);
        loadChats();
    }
});

els.sendForm.addEventListener("submit", (e) => {
    e.preventDefault();
    sendMessage();
});

els.messageInput.addEventListener("keydown", (e) => {
    if (e.key === "Enter" && !e.shiftKey) {
        e.preventDefault();
        sendMessage();
    }
});

els.createChatForm.addEventListener("submit", async e => {
    e.preventDefault();
    const name = document.getElementById("newChatName").value;
    const chatName = name.trim() || "Новый чат";

    const res = await fetch("/api/chat", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: chatName, type: 0, participantIds: [] })
    });

    if (res.ok) {
        bootstrap.Modal.getInstance(document.getElementById("createChatModal")).hide();
        loadChats();
        document.getElementById("newChatName").value = "";
    } else {
        alert("Ошибка при создании чата");
    }
});

els.addParticipantForm.addEventListener("submit", async e => {
    e.preventDefault();
    const username = document.getElementById("addUserName").value;
    try {
        const userRes = await fetch(`/api/user?username=${username}`);
        if (!userRes.ok) throw new Error("Пользователь не найден");
        const user = await userRes.json();

        const res = await fetch(`/api/chat/${currentChatId}/participants`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ chatId: currentChatId, userId: user.id, role: 0 })
        });

        if (res.ok) {
            bootstrap.Modal.getInstance(document.getElementById("addParticipantModal")).hide();
            new bootstrap.Modal(document.getElementById('chatInfoModal')).show();
            document.getElementById("addUserName").value = "";
        } else {
            const err = await res.json();
            alert(err.message || "Ошибка добавления");
        }
    } catch (ex) {
        alert(ex.message);
    }
});

els.chatHeaderClickable.addEventListener("click", (e) => {
    if (currentChatId) {
        openChatInfo(currentChatId);
        new bootstrap.Modal(document.getElementById('chatInfoModal')).show();
    }
});

els.currentUserBlock.addEventListener("click", () => {
    new bootstrap.Modal(document.getElementById('myProfileModal')).show();
});

els.dropZone.addEventListener("dragenter", e => {
    e.preventDefault();
    if (currentChatId) els.dragOverlay.classList.remove("d-none");
    else els.dragOverlay.classList.add("d-none");
    els.dragOverlay.classList.add("d-flex");
});

els.dragOverlay.addEventListener("dragleave", e => {
    e.preventDefault();
    els.dragOverlay.classList.add("d-none");
    els.dragOverlay.classList.remove("d-flex");
});

els.dragOverlay.addEventListener("dragover", e => e.preventDefault());

els.dragOverlay.addEventListener("drop", e => {
    e.preventDefault();
    els.dragOverlay.classList.add("d-none");
    els.dragOverlay.classList.remove("d-flex");

    if (e.dataTransfer.files.length > 0) {
        handleFiles(e.dataTransfer.files);
    }
});

els.fileInput.addEventListener("change", e => {
    if (e.target.files.length > 0) {
        handleFiles(e.target.files);
    }
});

function handleFiles(fileList) {
    els.attachmentsPreview.classList.remove("d-none");
    Array.from(fileList).forEach(file => {
        const reader = new FileReader();
        reader.onload = (e) => {
            const base64 = e.target.result;
            let type = 0;
            if (file.type.startsWith("image/")) type = 1;
            else if (file.type.startsWith("audio/")) type = 2;
            else if (file.type.startsWith("video/")) type = 3;

            const attachment = { type: type, url: base64, name: file.name, size: file.size };
            pendingAttachments.push(attachment);
            renderAttachmentPreview(attachment, pendingAttachments.length - 1);
        };
        reader.readAsDataURL(file);
    });
}

function renderAttachmentPreview(att, index) {
    const div = document.createElement("div");
    div.className = "position-relative border rounded overflow-hidden flex-shrink-0";
    div.style.width = "60px";
    div.style.height = "60px";

    let content = `<i class="bi bi-file-earmark fs-2 text-secondary d-block text-center mt-2"></i>`;
    if (att.type === 1) content = `<img src="${att.url}" class="w-100 h-100" style="object-fit:cover;">`;

    div.innerHTML = `
        ${content}
        <button type="button" class="btn-close position-absolute top-0 end-0 bg-white p-1" 
                style="width:8px; height:8px;" onclick="removeAttachment(${index}, this)"></button>
    `;
    els.attachmentsPreview.appendChild(div);
}

window.removeAttachment = function (index, btn) {
    btn.parentElement.remove();
    delete pendingAttachments[index];
    if (els.attachmentsPreview.children.length === 0) {
        els.attachmentsPreview.classList.add("d-none");
        pendingAttachments = [];
    }
};

function renderMyProfile(u) {
    const avatar = getAvatar(u.avatarUrl, u.displayName || u.userName);
    const name = u.displayName || u.userName;
    const username = u.userName;

    els.currentUserBlock.innerHTML = `
        <img src="${avatar}" class="rounded-circle" width="38" height="38">
        <div class="overflow-hidden">
            <div class="fw-bold text-truncate" style="font-size: 0.9rem;">${name}</div>
            <div class="small text-muted text-truncate" style="font-size: 0.75rem;">@${username}</div>
        </div>
        <div class="ms-auto text-secondary"><i class="bi bi-gear-fill"></i></div>
    `;

    els.myProfileModalContent.innerHTML = `
        <img src="${avatar}" class="rounded-circle mb-3 border p-1" width="100" height="100">
        <h4>${name}</h4>
        <p class="text-muted">@${username}</p>
        <div class="badge bg-success mb-3">Online</div>
        <p class="small text-muted">В сети с: ${new Date(u.lastOnline || new Date()).toLocaleTimeString()}</p>
    `;
}

function loadChats() {
    fetch("/api/chat/my").then(r => r.json()).then(chats => {
        els.chatsList.innerHTML = "";
        chats.forEach(chat => {
            const isActive = chat.id === currentChatId ? 'active' : '';
            const lastMsgText = chat.lastMessage ? chat.lastMessage.text : 'Нет сообщений';
            const time = chat.lastMessage ? new Date(chat.lastMessage.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '';

            let displayName = chat.name;
            let displayAvatar = getAvatar(null, chat.name);
            const count = chat.participants ? chat.participants.length : 1;

            if (chat.type === 1) {
                const otherUser = chat.participants.find(p => p.userId !== myUserId);
                if (otherUser) {
                    displayName = otherUser.displayName || otherUser.userName;
                    displayAvatar = getAvatar(null, displayName);
                }
            }

            const div = document.createElement("div");
            div.className = `list-group-item chat-item py-3 ${isActive}`;
            div.id = `chat-item-${chat.id}`;
            div.onclick = () => selectChat(chat, div, displayName, displayAvatar, count);

            div.innerHTML = `
                <div class="d-flex align-items-center gap-3">
                    <img src="${displayAvatar}" class="rounded-circle bg-white" width="48" height="48" style="object-fit:cover;">
                    <div class="flex-grow-1 overflow-hidden">
                        <div class="d-flex justify-content-between align-items-center">
                            <span class="chat-name fw-bold text-truncate">${displayName}</span>
                            <small class="text-muted" style="font-size:0.75em">${time}</small>
                        </div>
                        <small class="text-muted text-truncate d-block" id="last-msg-${chat.id}">
                            ${lastMsgText}
                        </small>
                    </div>
                </div>
            `;
            els.chatsList.appendChild(div);
        });
    });
}

function selectChat(chat, element, nameOverride, avatarOverride, count) {
    if (currentChatId === chat.id) return;
    currentChatId = chat.id;

    document.querySelectorAll(".chat-item").forEach(i => i.classList.remove("active"));
    if (element) element.classList.add("active");

    els.placeholder.classList.add("d-none");
    els.placeholder.classList.remove("d-flex");
    els.chatContent.classList.remove("d-none");
    els.chatContent.classList.add("d-flex");

    els.chatTitle.textContent = nameOverride || chat.name;
    els.chatAvatarHeader.src = avatarOverride || getAvatar(null, chat.name);

    const onlineText = (chat.type === 1) ? "" : `${count} участников`;
    els.chatHeaderStatus.textContent = onlineText;

    els.messagesList.innerHTML = "";
    lastSenderId = null;
    els.messageInput.value = "";
    els.messageInput.focus();

    pendingAttachments = [];
    els.attachmentsPreview.innerHTML = "";
    els.attachmentsPreview.classList.add("d-none");

    connection.invoke("JoinChat", chat.id);

    fetch(`/api/chat/${chat.id}/messages`)
        .then(r => r.json())
        .then(messages => {
            messages.forEach(renderMessage);
            scrollToBottom();
        });
}

function renderMessage(m) {
    const isMine = m.senderId === myUserId;
    const sameSender = lastSenderId === m.senderId;
    lastSenderId = m.senderId;

    const wrapper = document.createElement("div");
    wrapper.className = `d-flex gap-2 ${isMine ? "flex-row-reverse" : "flex-row"} ${sameSender ? "mt-1" : "mt-3"}`;

    const avatarVisibility = (!isMine && !sameSender) ? "visible" : "hidden";
    const avatarSrc = getAvatar(null, m.senderName);

    const avatarHtml = `
        <img src="${avatarSrc}" class="rounded-circle profile-link" width="32" height="32" 
             style="visibility: ${avatarVisibility};" 
             data-id="${m.senderId}">
    `;

    let nameHtml = "";
    if (!isMine && !sameSender) {
        nameHtml = `<div class="small text-muted ms-1 mb-1 profile-link" data-id="${m.senderId}">${m.senderName}</div>`;
    }

    let attachmentsHtml = "";
    if (m.attachments && m.attachments.length > 0) {
        attachmentsHtml = `<div class="d-flex flex-wrap gap-2 mb-2">`;
        m.attachments.forEach(att => {
            if (att.type === 1) {
                attachmentsHtml += `
                    <a href="${att.url}" target="_blank">
                        <img src="${att.url}" class="rounded border" style="max-width: 200px; max-height: 200px; object-fit: cover;">
                    </a>`;
            } else {
                attachmentsHtml += `
                    <a href="${att.url}" download="${att.name}" target="_blank" 
                       class="btn btn-sm btn-light border d-flex align-items-center gap-2 text-decoration-none text-dark" 
                       style="max-width: 200px;" title="${att.name}">
                        <i class="bi bi-file-earmark-arrow-down-fill text-primary fs-5"></i>
                        <div class="text-truncate" style="max-width: 140px;">${att.name}</div>
                    </a>`;
            }
        });
        attachmentsHtml += `</div>`;
    }

    const time = new Date(m.sentAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

    wrapper.innerHTML = `
        ${isMine ? '' : avatarHtml} 
        <div style="max-width: 100%; display: flex; flex-direction: column; align-items: ${isMine ? 'flex-end' : 'flex-start'}">
            ${nameHtml}
            <div class="message-bubble ${isMine ? "message-mine" : "message-other"}">
                ${attachmentsHtml}
                <div class="mb-1">${m.text || ""}</div>
                <div class="text-end opacity-75" style="font-size: 0.7em; margin-bottom: -4px;">
                    ${time}
                </div>
            </div>
        </div>
    `;

    els.messagesList.appendChild(wrapper);
}

function sendMessage() {
    const text = els.messageInput.value.trim();
    const validAttachments = pendingAttachments.filter(a => a);
    if ((!text && validAttachments.length === 0) || !currentChatId) return;

    els.messageInput.value = "";
    pendingAttachments = [];
    els.attachmentsPreview.innerHTML = "";
    els.attachmentsPreview.classList.add("d-none");

    fetch(`/api/chat/${currentChatId}/messages`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ text: text, attachments: validAttachments })
    }).catch(() => els.messageInput.value = text);
}

function updateLastMessageInList(chatId, text, time) {
    const el = document.getElementById(`last-msg-${chatId}`);
    if (el) el.textContent = text || "Вложение";
}

function scrollToBottom() {
    els.messagesContainer.scrollTop = els.messagesContainer.scrollHeight;
}

function openChatInfo(chatId) {
    fetch(`/api/chat/${chatId}`).then(r => r.json()).then(chat => {
        els.participantsList.innerHTML = "";
        chat.participants.forEach(p => {
            const roleRu = roleNames[p.role] || p.role;
            const li = document.createElement("li");
            li.className = "list-group-item d-flex align-items-center gap-3 px-0 border-0";
            li.innerHTML = `
                <img src="${getAvatar(null, p.displayName || p.userName)}" class="rounded-circle profile-link" 
                     width="36" height="36" data-id="${p.userId}">
                <div class="flex-grow-1">
                    <div class="fw-bold profile-link" data-id="${p.userId}">${p.displayName || p.userName}</div>
                    <small class="text-muted">@${p.userName}</small>
                </div>
                <span class="badge bg-light text-dark border">${roleRu}</span>
            `;
            els.participantsList.appendChild(li);
        });
    });
}

document.addEventListener("click", e => {
    const target = e.target.closest(".profile-link");
    if (target) {
        const userId = target.dataset.id;
        if (userId) openProfile(userId);
    }
});

function openProfile(userId) {
    fetch(`/api/user/${userId}`).then(r => r.json()).then(u => {
        const writeMsgBtn = (u.id !== myUserId)
            ? `<div class="d-grid"><button onclick="createPrivateChat('${u.id}')" class="btn btn-outline-primary btn-sm">Написать сообщение</button></div>`
            : '';
        els.profileContent.innerHTML = `
            <img src="${getAvatar(u.avatarUrl, u.displayName || u.userName)}" class="rounded-circle mb-3 shadow-sm" width="90" height="90">
            <h5 class="fw-bold">${u.displayName || u.userName}</h5>
            <p class="text-muted mb-2">@${u.userName}</p>
            <div class="badge ${u.isOnline ? 'bg-success' : 'bg-secondary'} mb-3">
                ${u.isOnline ? 'Online' : 'Offline'}
            </div>
            ${writeMsgBtn}
        `;
        new bootstrap.Modal(document.getElementById('profileModal')).show();
    });
}

window.createPrivateChat = async function (targetUserId) {
    const res = await fetch("/api/chat", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: "Private", type: 1, participantIds: [parseInt(targetUserId)] })
    });

    if (res.ok) {
        const chat = await res.json();
        bootstrap.Modal.getInstance(document.getElementById('profileModal')).hide();
        await loadChats();
        setTimeout(() => {
            const chatEl = document.getElementById(`chat-item-${chat.id}`);
            if (chatEl) chatEl.click();
        }, 100);
    } else {
        const err = await res.json();
        alert(err.message || "Ошибка при создании чата");
    }
};

window.logout = function () {
    fetch("/api/user/logout", { method: "POST" })
        .then(() => window.location.href = "/");
};