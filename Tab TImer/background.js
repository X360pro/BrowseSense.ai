// Global variables to track the active tab’s session details
let activeTabId = null;
let activeTabStartTime = null;
let activeTabUrl = null;
let activeTabTitle = null;
let siteDurations = {};

// ─────────────────────────────────────────────────────────────────────────────
// 1) Create alarms to poll for tab changes & upload sessions
// ─────────────────────────────────────────────────────────────────────────────
chrome.runtime.onInstalled.addListener(() => {
  // Poll active tab every ~5 seconds
  chrome.alarms.create('tabCheckAlarm', { periodInMinutes: 0.0833 });
  // Upload sessions to server every 1 minute
  chrome.alarms.create('uploadSessions', { periodInMinutes: 1 });
});

chrome.runtime.onStartup.addListener(() => {
  chrome.alarms.create('tabCheckAlarm', { periodInMinutes: 0.0833 });
  chrome.alarms.create('uploadSessions', { periodInMinutes: 1 });
});

// ─────────────────────────────────────────────────────────────────────────────
// 2) Tab activation: finalize old tab & start timer for new session
// ─────────────────────────────────────────────────────────────────────────────
chrome.tabs.onActivated.addListener(async ({ tabId }) => {
  const tab = await chrome.tabs.get(tabId);
  if (!tab.url) return;
  const urlObj = new URL(tab.url);
  const title = tab.title;

  // If this activation changes the tab or its title, finalize the old session
  if (activeTabId !== tabId || activeTabTitle !== title) {
    await finalizePreviousTab(tabId, tab.url, title);
  }
});

// ─────────────────────────────────────────────────────────────────────────────
// 3) Tab updates: detect URL or title changes and finalize if needed
// ─────────────────────────────────────────────────────────────────────────────
chrome.tabs.onUpdated.addListener(async (tabId, changeInfo, tab) => {
  if (!tab.url || !tab.active) return;
  // When the tab finishes loading or its title changes, check if details differ.
  if ((changeInfo.status === 'complete' && changeInfo.url) ||
      (changeInfo.title && activeTabTitle !== tab.title)) {
    await finalizePreviousTab(tabId, tab.url, tab.title);
  }
});

// ─────────────────────────────────────────────────────────────────────────────
// Update the duration every second
// ─────────────────────────────────────────────────────────────────────────────
function updateSiteDuration() {
  if (activeTabUrl) {
    const site = new URL(activeTabUrl).hostname;
    if (!siteDurations[site]) {
      siteDurations[site] = 0;
    }
    siteDurations[site] += 1; // Increment duration by 1 second
    console.log(`Updated duration for ${site}: ${siteDurations[site]} seconds`);
  }
}
setInterval(updateSiteDuration, 1000);

// ─────────────────────────────────────────────────────────────────────────────
// 4) Tab removal: finalize session if the active tab is closed
// ─────────────────────────────────────────────────────────────────────────────
chrome.tabs.onRemoved.addListener(async (tabId) => {
  if (tabId === activeTabId) {
    const endTime = Date.now();
    const durationMs = endTime - activeTabStartTime;
    // Use the stored details rather than fetching updated tab info.
    await storeSession(activeTabUrl, activeTabTitle, endTime, durationMs);
    activeTabId = null;
    activeTabStartTime = null;
    activeTabUrl = null;
    activeTabTitle = null;
  }
});

// ─────────────────────────────────────────────────────────────────────────────
// 5) onAlarm: check for tab changes while worker was asleep or upload data
// ─────────────────────────────────────────────────────────────────────────────
chrome.alarms.onAlarm.addListener(async (alarm) => {
  if (alarm.name === 'tabCheckAlarm') {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab || !tab.url) return;
    if (tab.id !== activeTabId) {
      await finalizePreviousTab(tab.id, tab.url, tab.title);
    }
  } else if (alarm.name === 'uploadSessions') {
    uploadSessionsToServer();
  }
});

// ─────────────────────────────────────────────────────────────────────────────
// 6) onSuspend: finalize current tab if the service worker is about to sleep
// ─────────────────────────────────────────────────────────────────────────────
chrome.runtime.onSuspend.addListener(async () => {
  if (activeTabId !== null) {
    const endTime = Date.now();
    const durationMs = endTime - activeTabStartTime;
    await storeSession(activeTabUrl, activeTabTitle, endTime, durationMs);
    activeTabId = null;
    activeTabStartTime = null;
    activeTabUrl = null;
    activeTabTitle = null;
  }
});

// ─────────────────────────────────────────────────────────────────────────────
// 7) finalizePreviousTab: finalize the old session and start a new one
// ─────────────────────────────────────────────────────────────────────────────
async function finalizePreviousTab(newActiveTabId, newUrl, newTitle) {
  if (activeTabId !== null && activeTabStartTime !== null) {
    const endTime = Date.now();
    const durationMs = endTime - activeTabStartTime;
    try {
      // Use the stored URL and title (old session details)
      await storeSession(activeTabUrl, activeTabTitle, endTime, durationMs);
    } catch (err) {
      console.error('Could not finalize previous tab info:', err);
      await storeSession('[Unknown URL]', '[Unknown Title]', endTime, durationMs);
    }
  }
  // Start a new session with the new details
  activeTabId = newActiveTabId;
  activeTabStartTime = Date.now();
  activeTabUrl = newUrl;
  activeTabTitle = newTitle;
}

// ─────────────────────────────────────────────────────────────────────────────
// 8) storeSession: Save session details (date & duration in seconds)
// ─────────────────────────────────────────────────────────────────────────────
async function storeSession(url, title, endTime, durationMs) {
  const durationSec = Math.floor(durationMs / 1000);
  const endDateObj = new Date(endTime);
  const year = endDateObj.getFullYear();
  const month = String(endDateObj.getMonth() + 1).padStart(2, '0');
  const day = String(endDateObj.getDate()).padStart(2, '0');
  const dateUsed = `${year}-${month}-${day}`;


  const data = await chrome.storage.local.get(['sessions']);
  const sessions = data.sessions || [];

  sessions.push({
    title,
    url,
    dateUsed,
    durationSec,
    timestamp: endTime
  });

  await chrome.storage.local.set({ sessions });
}

// ─────────────────────────────────────────────────────────────────────────────
// 9) uploadSessionsToServer: POST local sessions to your Node server
// ─────────────────────────────────────────────────────────────────────────────
async function uploadSessionsToServer() {
  try {
    const data = await chrome.storage.local.get(['sessions']);
    const sessions = data.sessions || [];

    if (sessions.length === 0) {
      console.log('No sessions to upload');
      return;
    }

    const response = await fetch('http://localhost:3000/api/storeSessions', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sessions })
    });

    if (response.ok) {
      console.log('Sessions uploaded successfully');
      await chrome.storage.local.set({ sessions: [] });
    } else {
      console.error('Error uploading sessions:', response.statusText);
    }
  } catch (error) {
    console.error('Error in uploadSessionsToServer:', error);
  }
}
