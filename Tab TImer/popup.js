// popup.js

// document.addEventListener('DOMContentLoaded', async () => {
//     try {
//       // 1) Retrieve sessions from local storage
//       const data = await chrome.storage.local.get('sessions');
//       const sessions = data.sessions || [];
//       const container = document.getElementById('sessions');
  
//       // 2) If no sessions, show a message
//       if (sessions.length === 0) {
//         container.textContent = 'No sessions recorded yet.';
//         return;
//       }
  
//       // 3) Create a list to display sessions
//       const list = document.createElement('ul');
  
//       sessions.forEach((session) => {
//         const li = document.createElement('li');
//         li.className = 'session-item';
  
//         // Extract fields from the session object
//         const title = session.title || '[Unknown Title]';
//         const url = session.url || '[Unknown URL]';
//         const dateUsed = session.dateUsed || '[No Date]';
//         const durationSec = session.durationSec ?? 0; // duration in seconds
  
//         // 4) Build structured HTML for each session
//         li.innerHTML = `
//           <div class="session-title">${title}</div>
//           <div class="session-detail">
//             <div><strong>Date:</strong> ${dateUsed}</div>
//             <div><strong>Duration:</strong> ${durationSec} seconds</div>
//             <div><strong>URL:</strong> ${url}</div>
//           </div>
//         `;
  
//         list.appendChild(li);
//       });
  
//       // 5) Clear loading text and append the list
//       container.innerHTML = '';
//       container.appendChild(list);
  
//     } catch (err) {
//       console.error('Error loading sessions:', err);
//     }
//   });

//   document.getElementById('redirect-button').addEventListener('click', function() {
//     // Redirect to the home page of the C# app
//     window.open('http://localhost:5172', '_blank');
// });
document.addEventListener('DOMContentLoaded', async () => {
  try {
      // 1) Retrieve sessions from local storage
      const data = await chrome.storage.local.get('sessions');
      const sessions = data.sessions || [];
      const container = document.getElementById('sessions');

      // 2) If no sessions, show a message
      if (sessions.length === 0) {
          container.textContent = 'No sessions recorded yet.';
          return;
      }

      // Sort sessions by dateUsed in descending order
       sessions.sort((a, b) => b.timestamp - a.timestamp);

      // 3) Create a list to display sessions
      const list = document.createElement('ul');

      sessions.forEach((session) => {
          const li = document.createElement('li');
          li.className = 'session-item';

          // Extract fields from the session object
          const title = session.title || '[Unknown Title]';
          const url = session.url || '[Unknown URL]';
          const dateUsed = session.dateUsed || '[No Date]';
          const durationSec = session.durationSec ?? 0; // duration in seconds

          // 4) Build structured HTML for each session
          li.innerHTML = `
              <div class="session-title">${title}</div>
              <div class="session-detail">
                  <div><strong>Date:</strong> ${dateUsed}</div>
                  <div><strong>Duration:</strong> ${durationSec} seconds</div>
                  <div><strong>URL:</strong> ${url}</div>
              </div>
          `;

          list.appendChild(li);
      });

      // 5) Clear loading text and append the list
      container.innerHTML = '';
      container.appendChild(list);

  } catch (err) {
      console.error('Error loading sessions:', err);
  }
});

const loginButton = document.getElementById('loginButton');
const goToSiteButton = document.getElementById('goToSiteButton');
const messageDiv = document.getElementById('message');

// // Check if the user is logged in
// chrome.storage.sync.get(['isLoggedIn'], function(result) {
//     if (result.isLoggedIn) {
//         messageDiv.textContent = 'Welcome back!';
//         goToSiteButton.style.display = 'block';
//         loginButton.style.display = 'none';
//     } else {
//         messageDiv.textContent = 'Please log in to use this extension.';
//         goToSiteButton.style.display = 'none';
//         loginButton.style.display = 'block';
//     }
// });

// Event listener for the login button
// loginButton.addEventListener('click', function() {
//     // Simulate a login process
//     // In a real-world scenario, you would authenticate the user here
//     chrome.storage.sync.set({ isLoggedIn: true }, function() {
//         messageDiv.textContent = 'Login successful!';
//         goToSiteButton.style.display = 'block';
//         loginButton.style.display = 'none';
//     });
// });

// Event listener for the "Go to Site" button

  document.getElementById('redirect-button').addEventListener('click', function() {
    // Redirect to the home page of the C# app
    window.open('http://localhost:5172', '_blank');
});