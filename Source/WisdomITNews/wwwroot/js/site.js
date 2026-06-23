// Wisdom IT News — site.js
document.addEventListener('DOMContentLoaded', () => {
    // Highlight active nav
    const path = location.pathname;
    document.querySelectorAll('.nav-list a').forEach(a => {
        if (a.getAttribute('href') === path) a.classList.add('active');
    });
});

/* ============== [B] Hover preview popup ============== */
(function () {
    if (window.__hoverPreviewInit) return;
    window.__hoverPreviewInit = true;

    const HOVER_DELAY = 220;          // ms - tránh popup nháy khi lướt nhanh
    const VIEWPORT_GAP = 8;
    let popupEl = null;
    let showTimer = null;
    let activeCard = null;

    function createPopup() {
        const el = document.createElement('div');
        el.className = 'preview-popup';
        el.innerHTML =
            '<img alt="" />' +
            '<div class="pp-body">' +
                '<h4 class="pp-title"></h4>' +
                '<p class="pp-summary"></p>' +
            '</div>';
        document.body.appendChild(el);
        return el;
    }

    function escapeHtml(s) {
        return (s || '').replace(/[&<>"']/g, function (c) {
            return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c];
        });
    }

    function positionPopup(card) {
        if (!popupEl) return;
        const r = card.getBoundingClientRect();
        const pw = popupEl.offsetWidth || 280;
        const ph = popupEl.offsetHeight || 220;
        const sx = window.scrollX || window.pageXOffset;
        const sy = window.scrollY || window.pageYOffset;

        // Đặt mặc định cạnh phải card
        let left = r.right + sx + VIEWPORT_GAP;
        let top  = r.top + sy;

        // Nếu vượt viewport phải → đặt cạnh trái
        if (left + pw > window.innerWidth + sx - VIEWPORT_GAP) {
            left = r.left + sx - pw - VIEWPORT_GAP;
        }
        // Nếu vẫn âm (card nhỏ hẹp) → đặt phía dưới
        if (left < sx + VIEWPORT_GAP) {
            left = Math.max(sx + VIEWPORT_GAP, r.left + sx);
            top  = r.bottom + sy + VIEWPORT_GAP;
        }
        // Đảm bảo không vượt đáy viewport
        if (top + ph > sy + window.innerHeight - VIEWPORT_GAP) {
            top = Math.max(sy + VIEWPORT_GAP, sy + window.innerHeight - ph - VIEWPORT_GAP);
        }
        popupEl.style.left = left + 'px';
        popupEl.style.top  = top + 'px';
    }

    function showPopup(card) {
        if (!popupEl) popupEl = createPopup();
        const thumb   = card.getAttribute('data-preview-thumb') || '';
        const title   = card.getAttribute('data-preview-title') || '';
        const summary = card.getAttribute('data-preview-summary') || '';

        popupEl.querySelector('img').src = thumb;
        popupEl.querySelector('img').alt = title;
        popupEl.querySelector('.pp-title').textContent = title;
        popupEl.querySelector('.pp-summary').textContent = summary;

        popupEl.classList.add('show');
        // Position phải sau khi show vì cần offsetWidth
        requestAnimationFrame(function () { positionPopup(card); });
        activeCard = card;
    }

    function hidePopup() {
        if (popupEl) popupEl.classList.remove('show');
        activeCard = null;
    }

    function onEnter(e) {
        const card = e.target.closest('.js-hover-card');
        if (!card) return;
        clearTimeout(showTimer);
        showTimer = setTimeout(function () { showPopup(card); }, HOVER_DELAY);
    }

    function onLeave(e) {
        const card = e.target.closest('.js-hover-card');
        if (!card) return;
        clearTimeout(showTimer);
        hidePopup();
    }

    // Delegate để cards thêm động cũng hoạt động
    document.addEventListener('mouseover', onEnter, true);
    document.addEventListener('mouseout',  onLeave, true);
    window.addEventListener('scroll', function () {
        if (activeCard) positionPopup(activeCard);
    }, { passive: true });
})();

/* ============== [E] Chatbot widget ============== */
(function () {
    if (window.__chatbotInit) return;
    window.__chatbotInit = true;

    document.addEventListener('DOMContentLoaded', function () {
        const toggleBtn = document.getElementById('chatbot-toggle');
        const panel     = document.getElementById('chatbot-panel');
        const closeBtn  = document.getElementById('chatbot-close');
        const form      = document.getElementById('chatbot-form');
        const input     = document.getElementById('chatbot-input');
        const messages  = document.getElementById('chatbot-messages');
        if (!toggleBtn || !panel || !form || !input || !messages) return;

        function openPanel() {
            panel.classList.add('open');
            setTimeout(function () { input.focus(); }, 200);
        }
        function closePanel() {
            panel.classList.remove('open');
        }
        function isOpen() { return panel.classList.contains('open'); }

        function appendMsg(text, who) {
            const div = document.createElement('div');
            div.className = 'chatbot-msg ' + who;
            div.textContent = text;
            messages.appendChild(div);
            messages.scrollTop = messages.scrollHeight;
            return div;
        }

        toggleBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            isOpen() ? closePanel() : openPanel();
        });
        closeBtn.addEventListener('click', function (e) {
            e.stopPropagation();
            closePanel();
        });

        // Click ra ngoài → đóng
        document.addEventListener('click', function (e) {
            if (!isOpen()) return;
            if (panel.contains(e.target) || toggleBtn.contains(e.target)) return;
            closePanel();
        });
        // ESC để đóng
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && isOpen()) closePanel();
        });

        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const msg = (input.value || '').trim();
            if (!msg) return;
            appendMsg(msg, 'user');
            input.value = '';
            input.disabled = true;
            const typing = appendMsg('Đang trả lời…', 'typing');

            try {
                const res = await fetch('/article/chat', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ message: msg })
                });
                const data = await res.json();
                typing.remove();
                appendMsg(data.reply || 'Xin lỗi, không có phản hồi.', 'bot');
            } catch (err) {
                typing.remove();
                appendMsg('Lỗi kết nối. Vui lòng thử lại.', 'bot');
            } finally {
                input.disabled = false;
                input.focus();
            }
        });
    });
})();

/* ============== [F] Comment like / dislike / reply ============== */
(function () {
    if (window.__commentTreeInit) return;
    window.__commentTreeInit = true;

    document.addEventListener('DOMContentLoaded', function () {
        const tree = document.getElementById('commentList');
        if (!tree) return;

        async function postJSON(url, body) {
            const res = await fetch(url, {
                method: 'POST',
                headers: body ? { 'Content-Type': 'application/json' } : {},
                body: body ? JSON.stringify(body) : undefined
            });
            return res.json();
        }

        // Like / Dislike — delegate
        tree.addEventListener('click', async function (e) {
            const likeBtn = e.target.closest('.js-like-btn');
            const dislikeBtn = e.target.closest('.js-dislike-btn');
            const replyBtn = e.target.closest('.js-reply-btn');
            const cancelBtn = e.target.closest('.js-reply-cancel');

            if (likeBtn || dislikeBtn) {
                const btn = likeBtn || dislikeBtn;
                const id  = btn.getAttribute('data-comment-id');
                const url = likeBtn ? '/article/like-comment/' + id : '/article/dislike-comment/' + id;
                btn.disabled = true;
                try {
                    const data = await postJSON(url);
                    if (data.success) {
                        const node = btn.closest('.comment-node');
                        if (node) {
                            const lc = node.querySelector('.like-count');
                            const dc = node.querySelector('.dislike-count');
                            if (lc) lc.textContent = data.likeCount;
                            if (dc) dc.textContent = data.dislikeCount;
                            btn.classList.add('voted');
                        }
                    } else {
                        btn.title = data.message || 'Không thể bình chọn';
                    }
                } catch (err) {
                    console.warn('vote failed', err);
                } finally {
                    btn.disabled = false;
                }
                return;
            }

            if (replyBtn) {
                const id = replyBtn.getAttribute('data-comment-id');
                const form = tree.querySelector('.reply-form[data-parent-id="' + id + '"]');
                if (!form) return;
                // Close other open forms
                tree.querySelectorAll('.reply-form.show').forEach(f => { if (f !== form) f.classList.remove('show'); });
                form.classList.toggle('show');
                if (form.classList.contains('show')) {
                    const ta = form.querySelector('textarea');
                    if (ta) ta.focus();
                }
                return;
            }

            if (cancelBtn) {
                const form = cancelBtn.closest('.reply-form');
                if (form) form.classList.remove('show');
                return;
            }
        });

        // Submit reply form
        tree.addEventListener('submit', async function (e) {
            const form = e.target.closest('.reply-form');
            if (!form) return;
            e.preventDefault();
            const parentId = parseInt(form.getAttribute('data-parent-id'), 10);
            const articleId = window.ARTICLE_ID || 0;
            const name = (form.querySelector('.reply-name').value || '').trim();
            const email = (form.querySelector('.reply-email').value || '').trim();
            const text = (form.querySelector('.reply-text').value || '').trim();
            if (!name || !text) { alert('⚠️ Vui lòng điền tên và nội dung'); return; }

            const submitBtn = form.querySelector('button[type="submit"]');
            submitBtn.disabled = true;

            try {
                const data = await postJSON('/article/reply-comment', {
                    articleId, parentCommentId: parentId,
                    name, email, content: text
                });
                if (data.success) {
                    alert('✅ ' + (data.message || 'Đã gửi'));
                    form.querySelector('.reply-name').value = '';
                    form.querySelector('.reply-email').value = '';
                    form.querySelector('.reply-text').value = '';
                    form.classList.remove('show');
                } else {
                    alert('❌ ' + (data.message || 'Không gửi được'));
                }
            } catch (err) {
                alert('❌ Lỗi kết nối');
            } finally {
                submitBtn.disabled = false;
            }
        });
    });
})();

/* ============== [G] Share bar ============== */
(function () {
    if (window.__shareBarInit) return;
    window.__shareBarInit = true;

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('.share-bar').forEach(function (bar) {
            const copyBtn = bar.querySelector('.js-share-copy');
            const toast   = bar.querySelector('.share-toast');
            if (!copyBtn) return;

            copyBtn.addEventListener('click', async function () {
                const url = bar.getAttribute('data-share-url') || window.location.href;
                let ok = false;
                try {
                    if (navigator.clipboard && window.isSecureContext) {
                        await navigator.clipboard.writeText(url);
                        ok = true;
                    } else {
                        // Fallback cho HTTP / browser cũ
                        const ta = document.createElement('textarea');
                        ta.value = url; ta.style.position = 'fixed'; ta.style.opacity = '0';
                        document.body.appendChild(ta);
                        ta.select();
                        ok = document.execCommand('copy');
                        document.body.removeChild(ta);
                    }
                } catch (err) { ok = false; }

                if (toast) {
                    toast.textContent = ok ? '✅ Đã sao chép!' : '❌ Không sao chép được';
                    toast.classList.add('show');
                    setTimeout(function () { toast.classList.remove('show'); }, 2000);
                }
            });
        });
    });
})();

/* ============== [M] Feedback popup ============== */
(function () {
    if (window.__feedbackInit) return;
    window.__feedbackInit = true;

    document.addEventListener('DOMContentLoaded', function () {
        const toggle   = document.getElementById('feedbackToggle');
        const backdrop = document.getElementById('feedbackBackdrop');
        const closeBtn = document.getElementById('feedbackClose');
        const cancel   = document.getElementById('feedbackCancel');
        const form     = document.getElementById('feedbackForm');
        const msg      = document.getElementById('feedbackMsg');
        if (!toggle || !backdrop || !form) return;

        function open()  { backdrop.classList.add('show');  document.getElementById('fbDesc').focus(); }
        function close() { backdrop.classList.remove('show'); msg.textContent = ''; msg.className = 'feedback-msg'; }

        toggle.addEventListener('click', function (e) { e.preventDefault(); open(); });
        closeBtn.addEventListener('click', close);
        cancel.addEventListener('click', close);
        backdrop.addEventListener('click', function (e) {
            if (e.target === backdrop) close();
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && backdrop.classList.contains('show')) close();
        });

        form.addEventListener('submit', async function (e) {
            e.preventDefault();
            const type = (document.getElementById('fbType').value || 'other').trim();
            const desc = (document.getElementById('fbDesc').value || '').trim();
            if (!desc) {
                msg.textContent = 'Vui lòng nhập mô tả'; msg.className = 'feedback-msg error';
                return;
            }
            const submitBtn = form.querySelector('.fb-send');
            submitBtn.disabled = true;
            try {
                const res = await fetch('/gop-y', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        type, description: desc,
                        pageUrl: window.location.href
                    })
                });
                const data = await res.json();
                if (data.success) {
                    msg.textContent = '✅ ' + (data.message || 'Đã gửi');
                    msg.className = 'feedback-msg success';
                    document.getElementById('fbDesc').value = '';
                    setTimeout(close, 1500);
                } else {
                    msg.textContent = '❌ ' + (data.message || 'Không gửi được');
                    msg.className = 'feedback-msg error';
                }
            } catch (err) {
                msg.textContent = '❌ Lỗi kết nối';
                msg.className = 'feedback-msg error';
            } finally {
                submitBtn.disabled = false;
            }
        });
    });
})();
