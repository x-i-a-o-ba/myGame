const $ = (id) => document.getElementById(id);
const labels = {
  game_designer: "策划",
  unity_architect: "技术架构",
  art_director: "美术",
  performance_analyst: "性能规划",
  qa_analyst: "测试",
};
const initials = {
  game_designer: "策",
  unity_architect: "技",
  art_director: "美",
  performance_analyst: "性",
  qa_analyst: "测",
};
const roleOrder = ["game_designer", "unity_architect", "art_director", "performance_analyst", "qa_analyst"];
const sourceNames = {
  userMessage: "用户消息",
  agentMessage: "代理消息",
  collabAgentToolCall: "派工 / 交接",
  commandExecution: "工具活动",
  fileChange: "文件变更",
  plan: "计划",
};
const handoffNames = {
  spawnAgent: "主 AI 派工",
  sendMessage: "成员间转交",
  sendInput: "补充任务",
  followupTask: "追加任务",
  resumeAgent: "继续任务",
  interruptAgent: "中断任务",
  closeAgent: "关闭任务",
};
let state = { ready: false, error: null, run: null, threads: [], feed: [], roles: [] };
let currentView = "group";
let selectedRoom = null;
let sending = false;
let renderTimer = null;
const expanded = new Set();

function element(tag, className, text) {
  const result = document.createElement(tag);
  if (className) result.className = className;
  if (text !== undefined && text !== null) result.textContent = String(text);
  return result;
}

function clear(node) {
  while (node.firstChild) node.removeChild(node.firstChild);
}

function truncate(text, size) {
  const value = String(text || "");
  return value.length > size ? value.slice(0, size - 1) + "…" : value;
}

function timestamp(value) {
  if (!value) return "";
  const date = new Date(typeof value === "number" && value < 1e12 ? value * 1000 : value);
  return Number.isNaN(date.getTime()) ? "" : date.toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit" });
}

function threadStatus(thread) {
  const raw = thread?.status;
  if (typeof raw === "string") return raw;
  return raw?.type || "idle";
}

function threadName(thread) {
  if (!thread) return "主 AI";
  if (!thread.parentThreadId) return "主 AI";
  if (thread.agentRole && labels[thread.agentRole]) return labels[thread.agentRole];
  return thread.agentNickname || thread.name || "子 AI";
}

function threadById(id) {
  return (state.threads || []).find((thread) => thread.id === id);
}

function rootThread() {
  const id = state.run?.rootThreadId;
  return threadById(id) || (state.threads || []).find((thread) => !thread.parentThreadId);
}

function notice(message) {
  const box = $("notice");
  box.textContent = message;
  box.hidden = false;
}

function hideNotice() {
  $("notice").hidden = true;
}

function scheduleRender() {
  if (renderTimer) return;
  renderTimer = setTimeout(() => {
    renderTimer = null;
    render();
  }, 120);
}

function render() {
  renderConnection();
  renderGoal();
  renderRoster();
  renderMilestones();
  renderFeed();
  renderRooms();
  renderFlow();
  renderComposer();
}

function renderConnection() {
  const badge = $("connectionBadge");
  badge.classList.toggle("online", Boolean(state.ready));
  badge.classList.toggle("offline", !state.ready);
  badge.querySelector("span").textContent = state.ready ? "实时连接" : "连接中断";
  if (state.error) notice(state.error);
}

function renderGoal() {
  const goal = state.run?.goal || "";
  $("goalTitle").textContent = goal ? truncate(goal, 90) : "等你给一个方向";
  $("goalSubtext").textContent = goal
    ? "当前看板负责只读调查；项目原生主任务负责实现、验证与返工。"
    : "主 AI 会自行拆解、派工、审查交付，并在最后向你汇报。";
  $("rootId").textContent = state.run?.rootThreadId ? state.run.rootThreadId.slice(0, 8) : "尚未开始";
  $("rootId").title = state.run?.rootThreadId || "";
}

function renderRoster() {
  const host = $("roleRoster");
  clear(host);
  const roles = availableRoles();
  if (!roles.length) {
    host.append(element("p", "small-note", "等待读取项目角色配置。"));
    return;
  }
  roles.forEach((role) => {
    const row = element("div", "role-row");
    row.append(element("span", "role-icon", initials[role.name] || "AI"));
    const copy = element("div");
    copy.append(element("strong", null, labels[role.name] || role.name));
    copy.append(element("small", null, truncate(role.description || role.name, 56)));
    row.append(copy);
    host.append(row);
  });
}

function availableRoles() {
  return (state.roles || [])
    .sort((a, b) => (roleOrder.indexOf(a.name) + 1 || 999) - (roleOrder.indexOf(b.name) + 1 || 999));
}

function renderMilestones() {
  const root = rootThread();
  const children = (state.threads || []).filter((thread) => thread.parentThreadId);
  const rootItems = root?.items || [];
  const hasGoal = Boolean(state.run?.rootThreadId);
  const mainTalked = rootItems.some((item) => item.type === "agentMessage" && item.text);
  const dispatched = (state.feed || []).some((entry) => entry.kind === "collabAgentToolCall" && entry.tool === "spawnAgent") || children.length > 0;
  const childDelivered = children.some((thread) => (thread.items || []).some((item) => item.type === "agentMessage" && item.phase === "final_answer"));
  const mainFinal = rootItems.some((item) => item.type === "agentMessage" && item.phase === "final_answer");
  const steps = [
    { title: "方向", caption: hasGoal ? "已收到" : "等待输入", done: hasGoal },
    { title: "主 AI 拆解", caption: dispatched ? "已派工" : mainTalked ? "进行中" : "未开始", done: dispatched },
    { title: "子 AI 交付", caption: childDelivered ? "已有交付" : children.length ? "进行中" : "未开始", done: childDelivered },
    { title: "主 AI 审查", caption: mainFinal ? "已汇总" : childDelivered ? "待审查" : "未开始", done: mainFinal },
    { title: "向你汇报", caption: mainFinal ? "已汇报" : "未开始", done: mainFinal },
  ];
  const activeIndex = steps.findIndex((step) => !step.done && step.caption !== "未开始");
  const list = $("milestones");
  clear(list);
  steps.forEach((step, index) => {
    const item = element("li", step.done ? "done" : index === activeIndex ? "current" : "");
    item.append(element("b", null, step.title), element("small", null, step.caption));
    list.append(item);
  });
}

function actorFor(entry) {
  if (entry.kind === "userMessage") return "你";
  if (entry.actor && entry.actor !== "main" && entry.actor !== "user") {
    return labels[entry.actor] || entry.actor;
  }
  const thread = threadById(entry.threadId);
  return threadName(thread);
}

function renderFeed() {
  const host = $("feed");
  const shouldStick = host.scrollHeight - host.scrollTop - host.clientHeight < 80;
  const oldScroll = host.scrollTop;
  clear(host);
  const feed = (state.feed || [])
    .filter((entry) => entry.kind !== "turnStatus" && (entry.kind !== "collabAgentToolCall" || !["wait", "listAgents"].includes(entry.tool)))
    .map((entry, index) => ({ entry, index }))
    .sort((a, b) => (Date.parse(a.entry.at) || 0) - (Date.parse(b.entry.at) || 0) || a.index - b.index)
    .map(({ entry }) => entry);
  $("feedCount").textContent = feed.length + " 条动态";
  if (!feed.length) {
    const empty = element("div", "empty-state");
    const orbit = element("div", "empty-orbit");
    orbit.append(element("span", null, "●"));
    empty.append(orbit, element("h3", null, "先告诉主 AI，你想做什么"), element("p", null, "例如：“规划下一轮战斗 Demo 的敌人种类和美术方向”。"));
    host.append(empty);
    return;
  }
  feed.slice(-250).forEach((entry) => {
    const isUser = entry.kind === "userMessage";
    const isDispatch = entry.kind === "collabAgentToolCall";
    const isMain = entry.threadId === state.run?.rootThreadId && !isUser && !isDispatch;
    const row = element("article", "feed-entry" + (isUser ? " user-entry" : isMain ? " main-entry" : isDispatch ? " dispatch-entry" : ""));
    row.append(element("div", "avatar" + (isUser ? " user" : isDispatch ? " dispatch" : isMain ? "" : " agent"), isUser ? "你" : isDispatch ? "↗" : isMain ? "主" : (initials[threadById(entry.threadId)?.agentRole] || "AI")));
    const body = element("div");
    const meta = element("div", "entry-meta");
    meta.append(element("strong", null, actorFor(entry)));
    if (isDispatch) meta.append(element("span", "type-label", handoffNames[entry.tool] || "派工 / 交接"));
    else if (isMain && entry.phase === "final_answer") meta.append(element("span", "type-label", "主 AI 汇报"));
    else if (entry.status && entry.status !== "completed") meta.append(element("span", "type-label", entry.status));
    meta.append(element("time", null, timestamp(entry.at)));
    body.append(meta);
    const content = element("div", "message-content" + (String(entry.text || "").length > 650 && !expanded.has(entry.id) ? " clamped" : ""), entry.text || "");
    body.append(content);
    if (String(entry.text || "").length > 650) {
      const toggle = element("button", "expand-button", expanded.has(entry.id) ? "收起" : "展开全文");
      toggle.type = "button";
      toggle.addEventListener("click", () => {
        if (expanded.has(entry.id)) expanded.delete(entry.id);
        else expanded.add(entry.id);
        renderFeed();
      });
      body.append(toggle);
    }
    row.append(body);
    host.append(row);
  });
  host.scrollTop = shouldStick ? host.scrollHeight : oldScroll;
}

function visibleItems(thread) {
  return (thread.items || []).filter((item) => ["userMessage", "agentMessage", "collabAgentToolCall", "commandExecution", "fileChange", "plan"].includes(item.type));
}

function itemText(item) {
  if (item.text) return item.text;
  if (item.type === "collabAgentToolCall") return item.prompt || "主 AI 分派了任务。";
  if (item.type === "commandExecution") return item.command || "执行工具";
  if (item.type === "fileChange") return "文件变更记录";
  return "";
}

function roomItem(item) {
  const kind = item.type === "userMessage" ? "user" : item.type === "agentMessage" ? "message" : "tool";
  const box = element("div", "room-item " + kind);
  box.append(element("span", "mini-label", sourceNames[item.type] || item.type));
  box.append(element("div", "mini-text", itemText(item)));
  return box;
}

function renderRooms() {
  const threads = state.threads || [];
  const root = rootThread();
  const ordered = root ? [root, ...threads.filter((thread) => thread.id !== root.id)] : threads;
  $("agentCount").textContent = Math.max(0, ordered.length - (root ? 1 : 0)) + " 位子 AI";
  if (!ordered.length) {
    clear($("roomTabs"));
    $("roomDetail").replaceChildren(roomPlaceholder());
    $("splitRooms").replaceChildren(roomPlaceholder());
    return;
  }
  if (!ordered.some((thread) => thread.id === selectedRoom)) selectedRoom = root?.id || ordered[0].id;
  const tabs = $("roomTabs");
  clear(tabs);
  ordered.forEach((thread) => {
    const button = element("button", "room-tab" + (thread.id === selectedRoom ? " active" : ""));
    button.type = "button";
    button.append(element("i"), document.createTextNode(threadName(thread)));
    button.addEventListener("click", () => { selectedRoom = thread.id; renderRooms(); });
    tabs.append(button);
  });
  const selected = ordered.find((thread) => thread.id === selectedRoom);
  const detail = $("roomDetail");
  const oldScroll = detail.scrollTop;
  const shouldStick = detail.scrollHeight - oldScroll - detail.clientHeight < 60;
  clear(detail);
  const heading = element("div", "room-head");
  heading.append(element("h3", null, threadName(selected)), element("p", null, "任务 " + selected.id.slice(0, 8) + " · " + statusLabel(threadStatus(selected))));
  detail.append(heading);
  const content = element("div", "room-items");
  const items = visibleItems(selected);
  if (!items.length) content.append(element("p", "small-note", "正在等待该成员的可见消息。"));
  else items.slice(-100).forEach((item) => content.append(roomItem(item)));
  detail.append(content);
  detail.scrollTop = shouldStick ? detail.scrollHeight : oldScroll;

  const split = $("splitRooms");
  clear(split);
  ordered.forEach((thread) => {
    const card = element("article", "split-room");
    const top = element("div", "split-room-head");
    top.append(element("strong", null, threadName(thread)), element("span", null, statusLabel(threadStatus(thread))));
    card.append(top);
    const messages = element("div", "split-room-items");
    const list = visibleItems(thread);
    if (!list.length) messages.append(element("p", "small-note", "暂无可见消息。"));
    else list.slice(-40).forEach((item) => messages.append(roomItem(item)));
    card.append(messages);
    split.append(card);
  });
}

function roomPlaceholder() {
  const empty = element("div", "room-placeholder");
  empty.append(element("div", "room-symbol", "◎"), element("strong", null, "等候成员加入"), element("p", null, "主 AI 派出子代理后，独立会话会出现在这里。"));
  return empty;
}

function statusLabel(status) {
  if (status === "active" || status === "inProgress" || status === "running") return "进行中";
  if (status === "idle" || status === "completed") return "已交付 / 空闲";
  if (status === "notLoaded") return "历史任务";
  if (status === "systemError" || status === "failed") return "异常";
  return status || "待开始";
}

function flowNode(kind, title, note, extra) {
  const box = element("div", "flow-node " + kind + (extra || ""));
  box.append(element("span", "flow-label", kind === "main" ? "ORCHESTRATOR" : kind === "user" ? "DIRECTION / HANDOFF" : "SPECIALIST"));
  box.append(element("strong", null, title));
  box.append(element("small", null, note));
  return box;
}

function renderFlow() {
  const host = $("flowCanvas");
  clear(host);
  host.append(flowNode("user", "你的大方向", state.run ? truncate(state.run.goal, 115) : "等待输入目标和你关心的体验"));
  host.append(element("div", "flow-arrow", "↓"));
  host.append(flowNode("main", "主 AI", "自主拆解 · 按需派工 · 处理冲突"));
  host.append(element("div", "flow-arrow", "↓"));
  const branches = element("div", "flow-branches");
  const children = (state.threads || []).filter((thread) => thread.parentThreadId);
  if (children.length) {
    children.forEach((thread) => branches.append(flowNode("", threadName(thread), statusLabel(threadStatus(thread)))));
  } else {
    availableRoles().forEach((role) => branches.append(flowNode("", labels[role.name] || role.name, "待主 AI 选择", " standby")));
  }
  host.append(branches);
  host.append(element("div", "flow-arrow", "↓"));
  host.append(flowNode("main", "主 AI 交付审查", "核对范围、文件证据与角色结论；必要时退回补充"));
  host.append(element("div", "flow-arrow", "↓"));
  host.append(flowNode("user", "向你汇报 · 可选试玩", "功能验证由可写的项目主任务执行；本看板只读"));
  host.append(element("p", "flow-note", "所有连线表示派工与交接关系，不代表子代理互相看到对方的完整对话。"));
}

function renderComposer() {
  const hasRun = Boolean(state.run?.rootThreadId);
  const busy = ["active", "running", "inProgress", "starting"].includes(state.run?.status);
  $("newButton").hidden = !hasRun;
  $("stopButton").hidden = !hasRun || !busy;
  $("sendButton").disabled = sending || !state.ready || busy;
  $("newButton").disabled = sending || !state.ready;
  $("sendButton").firstChild.textContent = hasRun ? "继续告诉主 AI " : "交给主 AI ";
  $("goalInput").placeholder = hasRun
    ? "给主 AI 补充方向、反馈或新的约束。当前任务完成后可继续。"
    : "想让这支 AI 团队先推进什么？可以只说目标和你关心的体验。";
}

function setView(view) {
  currentView = view;
  document.body.classList.toggle("split-mode", view === "split");
  ["group", "split", "flow"].forEach((name) => {
    $(name + "View").hidden = name !== view;
  });
  document.querySelectorAll(".view-button").forEach((button) => {
    button.classList.toggle("active", button.dataset.view === view);
    button.setAttribute("aria-pressed", button.dataset.view === view ? "true" : "false");
  });
  if (view === "split") renderRooms();
  if (view === "flow") renderFlow();
}

async function postJson(url, body) {
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  const result = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(result.error || "请求失败（" + response.status + "）");
  return result;
}

async function submitGoal(mode) {
  const goal = $("goalInput").value.trim();
  if (!goal) {
    notice("先写下一句方向，主 AI 才能开始派工。");
    $("goalInput").focus();
    return;
  }
  sending = true; hideNotice(); renderComposer();
  try {
    await postJson("/api/goals", { goal, mode });
    $("goalInput").value = "";
    const response = await fetch("/api/state");
    if (response.ok) state = await response.json();
    scheduleRender();
  } catch (error) {
    notice(error.message || String(error));
  } finally {
    sending = false; renderComposer();
  }
}

function connectEvents() {
  const stream = new EventSource("/api/events");
  stream.onmessage = (message) => {
    try {
      state = JSON.parse(message.data);
      scheduleRender();
    } catch (error) {
      notice("收到无法读取的实时事件：" + error.message);
    }
  };
  stream.onerror = () => {
    state.ready = false;
    scheduleRender();
  };
}

document.querySelectorAll(".view-button").forEach((button) => button.addEventListener("click", () => setView(button.dataset.view)));
$("goalForm").addEventListener("submit", (event) => {
  event.preventDefault();
  submitGoal(state.run ? "continue" : "new");
});
$("newButton").addEventListener("click", () => submitGoal("new"));
$("stopButton").addEventListener("click", async () => {
  try { await postJson("/api/interrupt", {}); } catch (error) { notice(error.message || String(error)); }
});
$("attachButton").addEventListener("click", () => $("attachDialog").showModal());
$("cancelAttach").addEventListener("click", () => $("attachDialog").close());
$("attachForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const threadId = $("attachInput").value.trim();
  if (!threadId) return;
  try {
    await postJson("/api/attach", { threadId });
    $("attachDialog").close();
    $("attachInput").value = "";
    const response = await fetch("/api/state");
    if (response.ok) state = await response.json();
    scheduleRender();
  } catch (error) {
    notice(error.message || String(error));
    $("attachDialog").close();
  }
});

setView(currentView);
fetch("/api/state").then((response) => response.json()).then((value) => { state = value; scheduleRender(); }).catch((error) => notice("无法连接工作台服务：" + error.message));
connectEvents();
