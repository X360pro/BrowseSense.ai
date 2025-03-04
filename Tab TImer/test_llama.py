import time
import pyodbc
from azure.ai.inference import ChatCompletionsClient
from azure.ai.inference.models import SystemMessage, UserMessage
from azure.core.credentials import AzureKeyCredential
from config import (
    AZURE_ENDPOINT,
    AZURE_MODEL_NAME,
    AZURE_KEY,
    DB_CONN_STR
)

# ------------------
# 1. Azure LLM Config
# ------------------
client = ChatCompletionsClient(
    endpoint=AZURE_ENDPOINT,
    credential=AzureKeyCredential(AZURE_KEY)
)

# ------------------
# 2. Database Config
# ------------------
conn_str = DB_CONN_STR

# ------------------
# 3. Helper Function: LLM to Classify Title
# ------------------
def get_topic_for_title(title):
    """
    Ask the LLM for a single-word topic from the allowed list.
    Return '[Unknown Topic]' if it doesn't match one of those tags.
    """
    prompt = f"""You are provided with the title of a website. Analyze every word in the title carefully for clues about the website’s subject matter. Then, using only the tags from the following list:

{{Finance, Job, Tech, Programming, Browsing, Cooking, Sports, Health, Education, Entertainment, Travel, Science, Politics, Art, History, Lifestyle, Music, Nature, Business, Fashion, Gaming, Literature}}

determine the single, most appropriate tag for the website. If the title indicates that the page is simply the main homepage of a website (i.e., it is generic and not specific to any topic), then choose the tag "Browsing".

Your answer must consist of exactly one word from the list with no additional commentary.

Title: {title}
"""
    response = client.complete(
        messages=[
            SystemMessage(content="You are a helpful assistant."),
            UserMessage(content=prompt),
        ],
        temperature=1.0,
        top_p=1.0,
        max_tokens=1000,
        model=model_name
    )
    llm_response = response.choices[0].message.content.strip()
    valid_tags = {"Finance", "Job", "Tech", "Programming", "Browsing", "Cooking", "Sports", "Health", 
                  "Education", "Entertainment", "Travel", "Science", "Politics", "Art", "History", 
                  "Lifestyle", "Music", "Nature", "Business", "Fashion", "Gaming", "Literature"}
    return llm_response if llm_response in valid_tags else "[Unknown Topic]"

# ------------------
# 4. Main Aggregation Logic (Optimized)
# ------------------
def aggregate_and_deduplicate():
    """
    Process the Sessions table in two steps:
      1. For groups of rows with Topic IS NULL (by Title, DateUsed):
         - If multiple rows exist, update the row with the lowest Id (summing DurationSec and assigning the LLM topic),
           then delete the remaining rows.
         - If only one row exists, update it with the LLM topic.
      2. For groups with Topic IS NOT NULL (by Title, DateUsed) that still have duplicates:
         - Update the row with the lowest Id with the summed DurationSec,
           then delete the other rows.
    """
    with pyodbc.connect(conn_str) as conn:
        cursor = conn.cursor()
        changes_made = False

        # Step 1: Process rows with Topic IS NULL
        null_groups_query = """
            SELECT Title, DateUsed, COUNT(*) AS NumRows, SUM(DurationSec) AS TotalSec
            FROM Sessions
            WHERE Topic IS NULL
            GROUP BY Title, DateUsed
        """
        cursor.execute(null_groups_query)
        null_groups = cursor.fetchall()

        for group in null_groups:
            title, date_used, num_rows, total_sec = group

            # Get topic using LLM
            topic = get_topic_for_title(title)

            # Select the keep row (lowest Id) among those with Topic IS NULL
            cursor.execute("""
                SELECT TOP 1 Id
                FROM Sessions
                WHERE Title = ? AND DateUsed = ? AND Topic IS NULL
                ORDER BY Id ASC
            """, (title, date_used))
            keep_row = cursor.fetchone()
            if not keep_row:
                continue
            keep_id = keep_row[0]

            if num_rows > 1:
                # Merge multiple rows: update the keep row and delete the others.
                cursor.execute("""
                    UPDATE Sessions
                    SET DurationSec = ?, Topic = ?
                    WHERE Id = ?
                """, (total_sec, topic, keep_id))
                cursor.execute("""
                    DELETE FROM Sessions
                    WHERE Title = ? AND DateUsed = ? AND Topic IS NULL AND Id <> ?
                """, (title, date_used, keep_id))
                print(f"Merged {num_rows} null-topic rows for Title='{title}', DateUsed={date_used} into Id={keep_id} with TotalSec={total_sec} and Topic='{topic}'")
            else:
                # Single row: simply update its Topic.
                cursor.execute("""
                    UPDATE Sessions
                    SET Topic = ?
                    WHERE Id = ?
                """, (topic, keep_id))
                print(f"Classified single null-topic row Id={keep_id} for Title='{title}', DateUsed={date_used} with Topic='{topic}'")
            changes_made = True

        conn.commit()

        # Step 2: Process duplicate groups where Topic IS NOT NULL (even if topics differ)
        assigned_groups_query = """
            SELECT Title, DateUsed, COUNT(*) AS NumRows, SUM(DurationSec) AS TotalSec
            FROM Sessions
            WHERE Topic IS NOT NULL
            GROUP BY Title, DateUsed
            HAVING COUNT(*) > 1
        """
        cursor.execute(assigned_groups_query)
        assigned_groups = cursor.fetchall()

        for group in assigned_groups:
            title, date_used, num_rows, total_sec = group

            # Select the keep row (lowest Id) among those with Topic IS NOT NULL
            cursor.execute("""
                SELECT TOP 1 Id
                FROM Sessions
                WHERE Title = ? AND DateUsed = ? AND Topic IS NOT NULL
                ORDER BY Id ASC
            """, (title, date_used))
            keep_row = cursor.fetchone()
            if not keep_row:
                continue
            keep_id = keep_row[0]

            # Update the keep row's DurationSec to be the sum of all DurationSec for the group.
            cursor.execute("""
                UPDATE Sessions
                SET DurationSec = ?
                WHERE Id = ?
            """, (total_sec, keep_id))
            # Delete all other rows in the group.
            cursor.execute("""
                DELETE FROM Sessions
                WHERE Title = ? AND DateUsed = ? AND Topic IS NOT NULL AND Id <> ?
            """, (title, date_used, keep_id))
            print(f"Merged {num_rows} assigned-topic rows for Title='{title}', DateUsed={date_used} into Id={keep_id} with TotalSec={total_sec}")
            changes_made = True

        conn.commit()
        return changes_made

def main_loop():
    """
    Continuously run aggregate_and_deduplicate() every 60 seconds.
    """
    while True:
        try:
            changes = aggregate_and_deduplicate()
            if not changes:
                print("No changes made in this cycle.")
        except Exception as e:
            print("Error in aggregation loop:", e)
        time.sleep(60)

if __name__ == "__main__":
    main_loop()
