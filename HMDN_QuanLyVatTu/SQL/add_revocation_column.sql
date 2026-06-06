-- Kiểm tra và bổ sung cột IsRevoked vào bảng TicketDiscussions nếu chưa có
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('TicketDiscussions') AND name = 'IsRevoked'
)
BEGIN
    ALTER TABLE TicketDiscussions ADD IsRevoked BIT NOT NULL DEFAULT 0;
END
GO
