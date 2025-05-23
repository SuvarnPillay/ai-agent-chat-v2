import React, { useState, useRef, useEffect } from "react";
import ReactMarkdown from "react-markdown";

// Remove trailing slash to avoid double slashes in requests
// const apiBaseUrl = (process.env.REACT_APP_API_URL || "http://localhost:5000").replace(/\/+$/, "");
const apiBaseUrl = ("http://localhost:5000").replace(/\/+$/, "");

const ChatUI = ({ sendMessage }) => {
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState("");
  const [loading, setLoading] = useState(false);
  const [threadId, setThreadId] = useState(null);
  const chatEndRef = useRef(null);


  // Always get a backend threadId
  useEffect(() => {
    const getThread = async () => {
      let tid = localStorage.getItem("ai_thread_id");
      let retries = 0;
      const maxRetries = 10;
      while ((!tid || typeof tid !== 'string' || !tid.startsWith('thread')) && retries < maxRetries) {
        const resp = await fetch(`${apiBaseUrl}/api/chat/thread`, { method: "POST" });
        const data = await resp.json();
        tid = data.threadId;
        if (tid && typeof tid === 'string' && tid.startsWith('thread')) {
          localStorage.setItem("ai_thread_id", tid);
          break;
        }
        retries++;
        await new Promise(res => setTimeout(res, 300)); // wait 300ms before retry
      }
      if (!tid || typeof tid !== 'string' || !tid.startsWith('thread')) {
        setThreadId(null);
        alert('Failed to get a valid thread ID from the server after several attempts. Please try refreshing the page.');
        return;
      }
      setThreadId(tid);
    };
    getThread();
  }, []);

  useEffect(() => {
    if (chatEndRef.current) {
      chatEndRef.current.scrollIntoView({ behavior: "smooth" });
    }
  }, [messages, loading]);

  const handleSend = async () => {
    if (!input.trim() || loading || !threadId) return;
    const currentInput = input;
    setInput("");
    setMessages(msgs => [
      ...msgs,
      { role: "user", content: currentInput, timestamp: new Date().toLocaleTimeString() }
    ]);
    setLoading(true);
    try {
      let assistantText = null;
      let lastAssistant = null;
      let waited = 0;
      const pollInterval = 50; // ms
      const maxWait = 5000; // ms
      setMessages(msgs => {
        lastAssistant = [...msgs].reverse().find(m => m.role === "assistant");
        return msgs;
      });
      while (waited < maxWait) {
        const response = await sendMessage(currentInput, threadId);
        if (!lastAssistant || response !== lastAssistant.content) {
          assistantText = response;
          break;
        }
        await new Promise(res => setTimeout(res, pollInterval));
        waited += pollInterval;
      }
      if (assistantText) {
        setMessages(msgs => [
          ...msgs,
          { role: "assistant", content: assistantText, timestamp: new Date().toLocaleTimeString() }
        ]);
      }
    } finally {
      setLoading(false);
    }
  };

  const handleInputKeyDown = (e) => {
    if (e.key === "Enter" && !loading && threadId) handleSend();
  };

  const handleResetConversation = () => {
    localStorage.removeItem("ai_thread_id");
    window.location.reload();
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100vh", background: "radial-gradient(ellipse at top left, #232b3b 60%, #0f1123 100%)", fontFamily: 'Segoe UI, Arial, sans-serif', color: '#e0e6f8' }}>
      <header style={{ padding: "16px 24px", background: "rgba(30,40,60,0.95)", borderBottom: "1px solid #2e3750", fontWeight: "bold", display: "flex", justifyContent: "space-between", alignItems: "center", letterSpacing: 2, fontSize: 22, boxShadow: '0 2px 8px #0f1123' }}>
        <span style={{ color: "#7ee7ff", textShadow: "0 0 8px #7ee7ff, 0 0 2px #fff" }}>AI Assistant</span>
        <button onClick={handleResetConversation} style={{ background: "linear-gradient(90deg,#7ee7ff,#3a8dde)", color: "#0f1123", border: "none", borderRadius: "8px", padding: "6px 20px", cursor: "pointer", fontWeight: "bold", boxShadow: "0 2px 8px #0f1123", letterSpacing: 1 }}>
          Reset
        </button>
      </header>
      <main style={{ flex: 1, overflowY: "auto", padding: "24px 0 16px 0", background: "none" }}>
        <div style={{ maxWidth: 700, margin: "0 auto" }}>
        {messages.map((msg, idx) => (
          <div key={idx} style={{ marginBottom: "22px", display: "flex", flexDirection: msg.role === "user" ? "row-reverse" : "row", alignItems: "flex-end" }}>
            <div style={{
              background: msg.role === "user" ? "linear-gradient(90deg,#7ee7ff,#3a8dde)" : "rgba(36,44,66,0.95)",
              color: msg.role === "user" ? "#0f1123" : "#e0e6f8",
              borderRadius: msg.role === "user" ? "18px 4px 18px 18px" : "4px 18px 18px 18px",
              padding: "14px 22px",
              maxWidth: "70%",
              minWidth: 60,
              fontSize: 17,
              boxShadow: msg.role === "user" ? "0 2px 12px #7ee7ff44" : "0 2px 12px #232b3b77",
              border: msg.role === "user" ? "1.5px solid #7ee7ff" : "1.5px solid #2e3750",
              marginLeft: msg.role === "user" ? 0 : 12,
              marginRight: msg.role === "user" ? 12 : 0,
              transition: "all 0.2s"
            }}>
              {msg.role === "assistant" ? (
                <ReactMarkdown>{msg.content}</ReactMarkdown>
              ) : (
                msg.content
              )}
            </div>
            <div style={{ fontSize: "0.8em", color: "#7ee7ff", margin: msg.role === "user" ? "0 12px 0 0" : "0 0 0 12px", letterSpacing: 1, textShadow: "0 0 4px #7ee7ff55" }}>
              {msg.role === "user" ? "You" : "AI"}
            </div>
          </div>
        ))}
        {loading && (
          <div style={{ color: "#7ee7ff", textAlign: "center", margin: "2em 0", fontSize: 18, letterSpacing: 1 }}>
            <div className="spinner" style={{ margin: "0 auto 1em", width: 40, height: 40, border: "5px solid #2e3750", borderTop: "5px solid #7ee7ff", borderRadius: "50%", animation: "spin 1s linear infinite", boxShadow: "0 2px 12px #7ee7ff44" }} />
            AI is thinking...
          </div>
        )}
        <div ref={chatEndRef} />
        </div>
      </main>
      <footer style={{ padding: "18px 0 18px 0", background: "rgba(30,40,60,0.95)", borderTop: "1px solid #2e3750", boxShadow: '0 -2px 8px #0f1123' }}>
        <div style={{ display: "flex", gap: "12px", maxWidth: 700, margin: "0 auto" }}>
          <input
            style={{ flex: 1, padding: "14px 18px", borderRadius: "14px", border: "1.5px solid #7ee7ff", background: "rgba(36,44,66,0.85)", color: "#e0e6f8", fontSize: 17, outline: "none", boxShadow: "0 2px 8px #0f112355" }}
            type="text"
            placeholder={threadId ? "Type your message..." : "Waiting for AI..."}
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleInputKeyDown}
            disabled={loading || !threadId}
            autoFocus
            aria-label="Type your message"
          />
          <button
            style={{ padding: "14px 28px", borderRadius: "14px", background: loading || !input.trim() || !threadId ? "#2e3750" : "linear-gradient(90deg,#7ee7ff,#3a8dde)", color: loading || !input.trim() || !threadId ? "#888" : "#0f1123", border: "none", fontWeight: "bold", opacity: loading || !input.trim() || !threadId ? 0.5 : 1, fontSize: 17, letterSpacing: 1, cursor: loading || !input.trim() || !threadId ? "not-allowed" : "pointer", boxShadow: loading ? "none" : "0 2px 8px #7ee7ff44" }}
            onClick={handleSend}
            disabled={loading || !input.trim() || !threadId}
            aria-label="Send message"
          >
            {loading ? "..." : "Send"}
          </button>
        </div>
      </footer>
      <style>{`
        @keyframes spin {
          0% { transform: rotate(0deg); }
          100% { transform: rotate(360deg); }
        }
      `}</style>
    </div>
  );
};

export default ChatUI;
