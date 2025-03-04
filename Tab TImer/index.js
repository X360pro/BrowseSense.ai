const express = require('express');
const sql = require('mssql');
const cors = require('cors');

const app = express();

// Enable CORS and JSON parsing
app.use(cors());
app.use(express.json());

// == Configure MS SQL Connection ==
const config = {
  user: 'SA',
  password: 'abhishekn25',
  server: 'localhost',
  port: 1433,
  database: 'master',
  options: {
    encrypt: false, // disable if local dev without TLS
    trustServerCertificate: true // or set to false if you have a proper cert
  }
};


app.post('/api/storeSessions', async (req, res) => {
  const { sessions } = req.body;
  
  if (!Array.isArray(sessions)) {
    return res.status(400).send('Invalid sessions data');
  }

  try {
    // 1) Connect to the database pool
    const pool = await sql.connect(config);

    // 2) Insert each session into the new columns
    for (const s of sessions) {
      const title = (s.title && s.title.substring(0, 100)) || '[Unknown Title]';
      const dateUsed = s.dateUsed || '1970-01-01'; // fallback date if missing
      const durationSec = Number(s.durationSec) || 0;
      const topic = null; // still null for now

      // Build the parameterized INSERT
      const insertQuery = `
        INSERT INTO Sessions (Title, Topic, DateUsed, DurationSec)
        VALUES (@title, @topic, @dateUsed, @durationSec)
      `;

      // 3) Create a new request and bind parameters
      await pool.request()
        .input('title', sql.NVarChar(100), title)
        .input('topic', sql.NVarChar(100), topic)
        .input('dateUsed', sql.Date, dateUsed)      // store as DATE (no time)
        .input('durationSec', sql.Int, durationSec) // store duration in seconds
        .query(insertQuery);
    }

    return res.status(200).send('Sessions inserted successfully');
  } catch (error) {
    console.error('Error inserting sessions:', error);
    return res.status(500).send('Error inserting sessions');
  }
});

// Start the server
app.listen(3000, () => {
  console.log('Server running on http://localhost:3000');
});
