(function () {
    'use strict';

    // ── DOM refs ──────────────────────────────────────────────────────────────
    const btn       = document.getElementById('chat-btn');
    const win       = document.getElementById('chat-window');
    const closeBtn  = document.getElementById('chat-close');
    const deleteBtn = document.getElementById('chat-delete'); // null khi chưa đăng nhập
    const msgs      = document.getElementById('chat-msgs');
    const input     = document.getElementById('chat-input');
    const sendBtn   = document.getElementById('chat-send');

    if (!btn || !win) return;

    const isLoggedIn = win.dataset.loggedIn === 'true';
    let isOpen   = false;
    let isBusy   = false;
    let historyLoaded = false;

    const GREETING =
        'Xin chào! Tôi là trợ lý AI của StarStay Hotel. ' +
        'Tôi có thể hỗ trợ bạn về giá phòng, phòng trống, dịch vụ và FAQ khách sạn. ' +
        'Bạn cần hỗ trợ gì?';

    // ── Quick-reply chips ─────────────────────────────────────────────────────
    const CHIPS = [
        { label: 'Giá phòng',  text: 'Giá phòng bao nhiêu?' },
        { label: 'Phòng trống', text: 'Hiện còn phòng trống không?' },
        { label: 'Dịch vụ',    text: 'Khách sạn có những dịch vụ gì?' },
    ];
    if (isLoggedIn) {
        CHIPS.push({ label: 'Đơn của tôi', text: 'Cho tôi xem đơn đặt phòng của tôi.' });
    }

    // ── Event listeners ───────────────────────────────────────────────────────
    btn.addEventListener('click', toggleChat);
    closeBtn.addEventListener('click', closeChat);
    if (deleteBtn) deleteBtn.addEventListener('click', deleteChat);
    // Bọc trong anonymous function để tránh MouseEvent bị truyền vào tham số textOverride
    sendBtn.addEventListener('click', function () { sendMsg(); });
    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); sendMsg(); }
    });

    // ── Core toggle ───────────────────────────────────────────────────────────
    function toggleChat() {
        isOpen ? closeChat() : openChat();
    }

    function openChat() {
        win.style.display = 'flex';
        isOpen = true;
        btn.innerHTML = '<i class="fas fa-times"></i>';
        if (!historyLoaded) {
            loadHistory();
            // Nạp model vào RAM ngầm để request đầu tiên không bị chậm
            fetch('/Chatbot/WarmUp').catch(function () {});
        }
        setTimeout(function () { input.focus(); }, 150);
    }

    function closeChat() {
        win.style.display = 'none';
        isOpen = false;
        btn.innerHTML = '<i class="fas fa-comments"></i>';
    }

    function deleteChat() {
        if (!confirm('Xóa toàn bộ lịch sử chat?')) return;
        fetch('/Chatbot/XoaLichSu', { method: 'POST' })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (data.ok) {
                    msgs.innerHTML = '';
                    historyLoaded = true;
                    appendBot(GREETING, true);
                    input.focus();
                }
            })
            .catch(function () {
                appendBot('Có lỗi khi xóa lịch sử. Vui lòng thử lại.', false);
            });
    }

    // ── Load lịch sử khi mở lần đầu ──────────────────────────────────────────
    function loadHistory() {
        historyLoaded = true;
        fetch('/Chatbot/LichSu')
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (!data || data.length === 0) {
                    appendBot(GREETING, true);
                } else {
                    data.forEach(function (m) {
                        if (m.vaiTro === 'User') appendUser(m.noiDung);
                        else appendBot(m.noiDung, false);
                    });
                    scrollDown();
                }
            })
            .catch(function () {
                appendBot(GREETING, true);
            });
    }

    // ── Gửi tin nhắn ─────────────────────────────────────────────────────────
    function sendMsg(textOverride) {
        var text = (textOverride || input.value).trim();
        if (!text || isBusy) return;
        input.value = '';
        appendUser(text);
        showTyping();

        fetch('/Chatbot/GuiAgent', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ cauHoi: text })
        })
        .then(function (r) { return r.json(); })
        .then(function (data) {
            hideTyping();
            appendBot(data.reply || 'Xin lỗi, có lỗi xảy ra. Vui lòng thử lại.', false);
        })
        .catch(function () {
            hideTyping();
            appendBot('Xin lỗi, không thể kết nối tới trợ lý. Vui lòng thử lại sau.', false);
        });
    }

    // ── Append messages ───────────────────────────────────────────────────────
    function appendUser(text) {
        var d = document.createElement('div');
        d.className = 'chat-msg chat-msg-user';
        d.textContent = text;
        msgs.appendChild(d);
        scrollDown();
    }

    function appendBot(html, showChips) {
        var d = document.createElement('div');
        d.className = 'chat-msg chat-msg-bot';
        d.innerHTML =
            '<span class="chat-bot-icon"><i class="fas fa-robot"></i></span>' +
            '<div class="chat-bot-text">' + toSafeHtml(html) + '</div>';
        msgs.appendChild(d);

        if (showChips) {
            var chips = document.createElement('div');
            chips.className = 'chat-chips';
            CHIPS.forEach(function (c) {
                var b = document.createElement('button');
                b.className = 'chat-chip';
                b.textContent = c.label;
                b.addEventListener('click', function () {
                    chips.remove();
                    sendMsg(c.text);
                });
                chips.appendChild(b);
            });
            msgs.appendChild(chips);
        }

        scrollDown();
    }

    // ── Typing indicator ──────────────────────────────────────────────────────
    function showTyping() {
        isBusy = true;
        sendBtn.disabled = true;
        input.disabled   = true;
        var d = document.createElement('div');
        d.className = 'chat-msg chat-msg-bot';
        d.id = 'chat-typing';
        d.innerHTML =
            '<span class="chat-bot-icon"><i class="fas fa-robot"></i></span>' +
            '<div class="chat-bot-text"><span class="typing-dot"></span>' +
            '<span class="typing-dot"></span><span class="typing-dot"></span></div>';
        msgs.appendChild(d);
        scrollDown();
    }

    function hideTyping() {
        isBusy = false;
        sendBtn.disabled = false;
        input.disabled   = false;
        var t = document.getElementById('chat-typing');
        if (t) t.remove();
        input.focus();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    function scrollDown() {
        msgs.scrollTop = msgs.scrollHeight;
    }

    // Renderer markdown tối giản: escape HTML trước, sau đó áp dụng markdown.
    // Hỗ trợ **bold**, `code`, bullet list (- / * đầu dòng), numbered list, newline=><br>.
    // Bỏ qua table (| col |) - render thành text thuần, đọc được không đẹp.
    function toSafeHtml(text) {
        if (!text) return '';
        var s = text
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
        // **bold**
        s = s.replace(/\*\*([^*\n]+)\*\*/g, '<strong>$1</strong>');
        // `inline code`
        s = s.replace(/`([^`\n]+)`/g, '<code style="background:#eee;padding:0 3px;border-radius:3px">$1</code>');
        // Bullet list: dòng bắt đầu bằng "- " hoặc "* "
        s = s.replace(/(^|\n)[*-] (.+)/g, '$1&bull; $2');
        // Newline => <br>
        s = s.replace(/\n/g, '<br>');
        return s;
    }
})();
