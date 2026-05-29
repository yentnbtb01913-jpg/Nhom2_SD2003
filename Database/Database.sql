-- WISDOM IT NEWS — Seed Data Mở Rộng
-- Bổ sung thêm bài viết, tags, comments với ảnh từ Unsplash

USE WisdomITNews;
GO

-- Xóa data cũ
DELETE FROM ArticleTags;
DELETE FROM Comments;
DELETE FROM AILogs;
DELETE FROM Articles;
DELETE FROM Tags;
GO

-- ===================== TAGS =====================
SET IDENTITY_INSERT Tags ON;
INSERT INTO Tags (Id, Name, Slug, CreatedAt) VALUES
(1,  N'Python',           'python',           GETDATE()),
(2,  N'JavaScript',       'javascript',       GETDATE()),
(3,  N'PHP',              'php',              GETDATE()),
(4,  N'Machine Learning', 'machine-learning', GETDATE()),
(5,  N'ChatGPT',          'chatgpt',          GETDATE()),
(6,  N'Docker',           'docker',           GETDATE()),
(7,  N'React',            'react',            GETDATE()),
(8,  N'MySQL',            'mysql',            GETDATE()),
(9,  N'Linux',            'linux',            GETDATE()),
(10, N'Bảo Mật',          'bao-mat-tag',      GETDATE()),
(11, N'AI',               'ai',               GETDATE()),
(12, N'TypeScript',       'typescript',       GETDATE()),
(13, N'Cloud',            'cloud',            GETDATE()),
(14, N'Git',              'git',              GETDATE()),
(15, N'VS Code',          'vs-code',          GETDATE()),
(16, N'API',              'api',              GETDATE()),
(17, N'Deep Learning',    'deep-learning',    GETDATE()),
(18, N'Node.js',          'nodejs',           GETDATE()),
(19, N'Flutter',          'flutter',          GETDATE()),
(20, N'Kubernetes',       'kubernetes',       GETDATE()),
(21, N'C#',               'csharp',           GETDATE()),
(22, N'ASP.NET',          'aspnet',           GETDATE()),
(23, N'SQL Server',       'sql-server',       GETDATE()),
(24, N'Vue.js',           'vuejs',            GETDATE()),
(25, N'Next.js',          'nextjs',           GETDATE()),
(26, N'MongoDB',          'mongodb',          GETDATE()),
(27, N'AWS',              'aws',              GETDATE()),
(28, N'Google Cloud',     'google-cloud',     GETDATE()),
(29, N'Blockchain',       'blockchain',       GETDATE()),
(30, N'ChatBot',          'chatbot',          GETDATE());
SET IDENTITY_INSERT Tags OFF;
GO

-- ===================== ARTICLES =====================
SET IDENTITY_INSERT Articles ON;
INSERT INTO Articles (Id,Title,Slug,Summary,Content,Thumbnail,CategoryId,AuthorId,Views,Status,IsFeatured,IsBreaking,PublishedAt,AiSummary,MetaTitle,MetaDesc,CreatedAt,UpdatedAt) VALUES

-- ===== AI & MACHINE LEARNING (CategoryId = 3) =====
(1,
N'Trí tuệ nhân tạo tạo sinh bước vào kỷ nguyên mới: Khi AI có thể tự học và lập luận như con người',
'tri-tue-nhan-tao-tao-sinh-buoc-vao-ky-nguyen-moi',
N'Năm 2025 đánh dấu bước ngoặt lớn trong lịch sử AI. Các mô hình ngôn ngữ lớn không chỉ trả lời mà đã bắt đầu lý luận, giải quyết bài toán phức tạp vượt khả năng nhiều chuyên gia.',
N'<p>Nếu năm 2023 là năm thế giới giật mình trước ChatGPT, thì năm 2025 chứng kiến bước tiến hoàn toàn khác: <strong>AI đang bắt đầu suy nghĩ thực sự</strong>.</p><h2>AI Tạo Sinh Là Gì?</h2><p>AI tạo sinh là nhóm mô hình có khả năng tạo ra nội dung mới — văn bản, hình ảnh, mã phần mềm — thay vì chỉ phân tích dữ liệu có sẵn.</p><blockquote>Chúng ta đang chứng kiến sự xuất hiện của một công cụ nhận thức mới — không chỉ là máy tính mạnh hơn, mà là đối tác tư duy thực sự.</blockquote><h2>Những Đột Phá Lớn 2025</h2><ul><li><strong>ChatGPT-5</strong> vượt 92% chuyên gia trong bài kiểm tra luật sư Mỹ</li><li><strong>Gemini Ultra 2.0</strong> giải 100% Olympic Toán quốc tế</li><li><strong>AlphaCode 3</strong> viết code tốt hơn 85% senior developer</li></ul><h2>Tác Động Đến Ngành IT</h2><p>Các nhà tuyển dụng đang thay đổi tiêu chí: không cần người viết code thuần túy, mà cần người <strong>biết hướng dẫn AI làm việc hiệu quả</strong>. Đây là kỹ năng mới mà mọi lập trình viên cần học.</p>',
'https://images.unsplash.com/photo-1677442136019-21780ecad995?w=800&q=80',
3,1,15420,'published',1,1,DATEADD(HOUR,-2,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-2,GETDATE()),GETDATE()),

(2,
N'Google Gemini Ultra 2.0 đạt điểm tuyệt đối trong benchmark lập trình quốc tế',
'google-gemini-ultra-2-dat-diem-tuyet-doi-benchmark-lap-trinh',
N'Google DeepMind công bố Gemini Ultra 2.0 đạt điểm tuyệt đối ở 48/50 benchmark lập trình, vượt xa mọi đối thủ hiện tại.',
N'<p>Trong công bố gây chấn động cộng đồng lập trình, <strong>Gemini Ultra 2.0</strong> hoàn thành xuất sắc loạt bài kiểm tra lập trình khắt khe nhất hiện nay.</p><h2>Kết Quả Benchmark</h2><ul><li>HumanEval: 98.5% (so với GPT-4o là 91.2%)</li><li>MBPP: 99.1%</li><li>SWE-Bench: giải được 72% bug thực tế trong các repo GitHub lớn</li></ul><h2>Điều Này Có Nghĩa Gì?</h2><p>Gemini Ultra 2.0 không chỉ viết code đúng cú pháp mà còn hiểu context, đề xuất kiến trúc và phát hiện lỗi tiềm ẩn — những việc vốn chỉ senior developer mới làm được.</p>',
'https://images.unsplash.com/photo-1620712943543-bcc4688e7485?w=800&q=80',
3,1,9240,'published',1,0,DATEADD(HOUR,-5,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-5,GETDATE()),GETDATE()),

(3,
N'So sánh Claude 3.5 vs GPT-4o: Cái nào tốt hơn cho lập trình viên?',
'so-sanh-claude-35-vs-gpt-4o-cho-lap-trinh-vien',
N'Phân tích chi tiết ưu nhược điểm của Claude 3.5 Sonnet và GPT-4o trong các tác vụ lập trình thực tế — từ viết code đến debug và giải thích.',
N'<p>Kể từ khi Anthropic ra mắt Claude 3.5 Sonnet, cộng đồng liên tục so sánh với GPT-4o của OpenAI. Mỗi mô hình có điểm mạnh riêng.</p><h2>Claude 3.5 Sonnet</h2><ul><li>Xuất sắc trong giải thích code dài, tái cấu trúc và review PR</li><li>Ít "ảo giác" hơn khi làm việc với codebase lớn</li><li>Giới hạn context window 200K token</li></ul><h2>GPT-4o</h2><ul><li>Xử lý đa phương tiện tốt hơn (ảnh, âm thanh, video)</li><li>Plugin ecosystem phong phú</li><li>Phản hồi nhanh hơn trong các tác vụ đơn giản</li></ul><h2>Kết Luận</h2><p>Với lập trình viên thuần code: <strong>Claude 3.5 nhỉnh hơn</strong>. Với người cần đa nhiệm: <strong>GPT-4o linh hoạt hơn</strong>.</p>',
'https://images.unsplash.com/photo-1526374965328-7f61d4dc18c5?w=800&q=80',
3,1,9720,'published',0,0,DATEADD(HOUR,-8,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-8,GETDATE()),GETDATE()),

(4,
N'Deep Learning 2025: 5 kiến trúc mạng nơ-ron đang thay đổi thế giới',
'deep-learning-2025-5-kien-truc-mang-no-ron',
N'Từ Transformer đến Mamba, các kiến trúc mạng nơ-ron mới đang phá vỡ giới hạn của AI. Cùng khám phá 5 kiến trúc đột phá nhất năm 2025.',
N'<p>Thế giới Deep Learning không ngừng biến đổi. Năm 2025, một loạt kiến trúc mạng nơ-ron mới đã ra đời, mỗi cái giải quyết những hạn chế mà người tiền nhiệm để lại.</p><h2>1. Mamba — Thách thức ngôi vương Transformer</h2><p>Mamba sử dụng State Space Models thay vì attention mechanism, xử lý chuỗi dài hiệu quả hơn O(n) so với O(n²) của Transformer.</p><h2>2. Vision Transformer (ViT) v2</h2><p>Áp dụng kiến trúc Transformer cho thị giác máy tính, đạt độ chính xác 99.2% trên ImageNet.</p><h2>3. Mixture of Experts (MoE)</h2><p>Chỉ kích hoạt một phần mạng cho mỗi input, tiết kiệm tài nguyên tính toán đáng kể.</p>',
'https://images.unsplash.com/photo-1555949963-aa79dcee981c?w=800&q=80',
3,1,4830,'published',0,0,DATEADD(HOUR,-12,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-12,GETDATE()),GETDATE()),

-- ===== LẬP TRÌNH (CategoryId = 2) =====
(5,
N'Top 10 thư viện Python không thể bỏ qua trong năm 2025',
'top-10-thu-vien-python-khong-the-bo-qua-2025',
N'Python vẫn là ngôn ngữ lập trình phổ biến nhất với hệ sinh thái khổng lồ. Đây là 10 thư viện bạn nhất định phải biết để tăng năng suất.',
N'<p>Python tiếp tục dẫn đầu ngôn ngữ lập trình phổ biến nhất theo Stack Overflow 2025.</p><h2>1. Pandas — Xử lý dữ liệu</h2><p>Pandas vẫn là công cụ không thể thiếu cho data scientist với DataFrame API mạnh mẽ.</p><h2>2. FastAPI — Web framework hiện đại</h2><p>FastAPI đang dần thay thế Flask nhờ hiệu năng vượt trội và hỗ trợ async/await.</p><h2>3. LangChain — Xây dựng ứng dụng AI</h2><p>LangChain là framework phổ biến nhất để xây dựng ứng dụng với LLM.</p><h2>4. Polars — Thay thế Pandas?</h2><p>Polars xử lý DataFrame nhanh hơn Pandas 10-100 lần nhờ viết bằng Rust.</p><h2>5. Pydantic v2</h2><p>Validation dữ liệu type-safe, được dùng trong FastAPI và nhiều framework lớn.</p>',
'https://images.unsplash.com/photo-1555066931-4365d14bab8c?w=800&q=80',
2,1,8230,'published',1,0,DATEADD(HOUR,-6,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-6,GETDATE()),GETDATE()),

(6,
N'Hướng dẫn xây dựng chatbot AI bằng Python và LangChain từ A đến Z',
'huong-dan-xay-dung-chatbot-ai-python-langchain',
N'LangChain là framework phổ biến nhất xây dựng ứng dụng AI. Bài viết hướng dẫn chi tiết xây dựng chatbot thông minh cho website trong 30 phút.',
N'<p>LangChain là framework Python mạnh mẽ giúp xây dựng ứng dụng với mô hình ngôn ngữ lớn, kết nối AI với database, file PDF, website.</p><h2>Cài đặt</h2><p>Bắt đầu với: <code>pip install langchain openai chromadb</code></p><h2>Tạo Chatbot Cơ Bản</h2><p>Sử dụng ConversationChain để duy trì lịch sử hội thoại giữa các lượt chat.</p><h2>Kết Nối Với Tài Liệu</h2><p>RAG (Retrieval Augmented Generation) cho phép chatbot trả lời dựa trên tài liệu của bạn.</p>',
'https://images.unsplash.com/photo-1607799279861-4dd421887fb3?w=800&q=80',
2,1,3180,'published',0,0,DATEADD(HOUR,-10,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-10,GETDATE()),GETDATE()),

(7,
N'Docker Compose vs Kubernetes: Khi nào nên dùng cái nào?',
'docker-compose-vs-kubernetes-khi-nao-dung-cai-nao',
N'Phân tích chi tiết Docker Compose và Kubernetes để giúp bạn chọn đúng giải pháp container orchestration cho dự án của mình.',
N'<p>Docker Compose và Kubernetes đều quản lý container nhưng phục vụ hai mục đích khác nhau ở các quy mô khác nhau.</p><h2>Docker Compose — Đơn Giản & Nhanh</h2><p>Phù hợp cho môi trường development và dự án nhỏ. Định nghĩa toàn bộ stack trong một file YAML đơn giản.</p><h2>Kubernetes — Mạnh Mẽ & Scalable</h2><p>Phù hợp production với hàng trăm container, auto-scaling, self-healing và zero-downtime deployment.</p><h2>Quy Tắc Chọn</h2><ul><li>Dev local / staging nhỏ: Docker Compose</li><li>Production / microservices lớn: Kubernetes</li><li>Đang học: bắt đầu với Compose rồi chuyển dần sang K8s</li></ul>',
'https://images.unsplash.com/photo-1618401471353-b98afee0b2eb?w=800&q=80',
2,1,3890,'published',0,0,DATEADD(HOUR,-14,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-14,GETDATE()),GETDATE()),

(8,
N'TypeScript 5.5: Những tính năng mới khiến JavaScript developer phải chú ý',
'typescript-5-5-tinh-nang-moi-noi-bat',
N'TypeScript 5.5 ra mắt với nhiều cải tiến đột phá về type inference, performance và developer experience. Cùng điểm qua những thay đổi quan trọng nhất.',
N'<p>TypeScript tiếp tục khẳng định vị thế là ngôn ngữ không thể thiếu trong hệ sinh thái JavaScript với bản cập nhật 5.5 đầy ấn tượng.</p><h2>Inferred Type Predicates</h2><p>TypeScript giờ có thể tự suy luận type predicate mà không cần khai báo thủ công — tiết kiệm hàng chục dòng boilerplate.</p><h2>Isolated Declarations</h2><p>Tăng tốc độ build đáng kể bằng cách cho phép các file được type-check song song.</p><h2>Regular Expression Syntax Checking</h2><p>TypeScript giờ kiểm tra cú pháp regex tại compile time, bắt lỗi sớm trước khi runtime.</p>',
'https://images.unsplash.com/photo-1579468118864-1b9ea3c0db4a?w=800&q=80',
2,1,5640,'published',0,0,DATEADD(HOUR,-18,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-18,GETDATE()),GETDATE()),

(9,
N'ASP.NET Core 9.0: Hiệu năng tăng vọt và các tính năng mới cho developer C#',
'aspnet-core-9-hieu-nang-tang-vot',
N'Microsoft vừa phát hành ASP.NET Core 9.0 với hàng loạt cải tiến hiệu năng, Blazor nâng cấp và minimal API mạnh mẽ hơn bao giờ hết.',
N'<p>ASP.NET Core 9.0 tiếp tục truyền thống cải tiến hiệu năng mạnh mẽ mà Microsoft đã duy trì qua các phiên bản trước.</p><h2>Hiệu Năng Vượt Trội</h2><p>Benchmark TechEmpower cho thấy ASP.NET Core 9.0 xử lý <strong>7.5 triệu request/giây</strong> — tăng 18% so với phiên bản 8.0.</p><h2>Blazor Unified Model</h2><p>Blazor giờ hỗ trợ render mode linh hoạt: Server, WebAssembly hoặc Auto tùy theo từng component.</p><h2>Minimal API Improvements</h2><p>Native AOT compilation hỗ trợ đầy đủ, giảm thời gian startup xuống còn vài millisecond.</p>',
'https://images.unsplash.com/photo-1542831371-29b0f74f9713?w=800&q=80',
2,1,4210,'published',0,0,DATEADD(HOUR,-20,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-20,GETDATE()),GETDATE()),

(10,
N'Git Flow vs GitHub Flow: Chiến lược branching nào phù hợp với team của bạn?',
'git-flow-vs-github-flow-chon-strategy-nao',
N'Hai chiến lược quản lý branch phổ biến nhất — Git Flow và GitHub Flow — mỗi cái có ưu điểm riêng. Bài viết giúp bạn chọn đúng cho dự án.',
N'<p>Quản lý branch tốt là nền tảng của teamwork hiệu quả. Git Flow và GitHub Flow là hai approach phổ biến nhất, nhưng chúng phù hợp với các loại dự án khác nhau.</p><h2>Git Flow</h2><p>Phù hợp cho sản phẩm có release cycle rõ ràng. Dùng nhiều branch: main, develop, feature/*, hotfix/*, release/*.</p><h2>GitHub Flow</h2><p>Đơn giản hơn, chỉ cần main + feature branch. Deploy liên tục, phù hợp CI/CD.</p><h2>Khuyến Nghị</h2><ul><li>Startup / web app CI/CD: GitHub Flow</li><li>Phần mềm versioned / mobile app: Git Flow</li></ul>',
'https://images.unsplash.com/photo-1556075798-4825dfaaf498?w=800&q=80',
2,1,2960,'published',0,0,DATEADD(HOUR,-22,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-22,GETDATE()),GETDATE()),

-- ===== TIN CÔNG NGHỆ (CategoryId = 1) =====
(11,
N'Apple M4 Pro chip: Hiệu năng AI tăng 40% so với thế hệ trước',
'apple-m4-pro-chip-hieu-nang-ai-tang-40-phan-tram',
N'Apple vừa ra mắt chip M4 Pro với Neural Engine mạnh mẽ, mang lại hiệu năng AI tăng 40% và tiêu thụ điện năng giảm đáng kể.',
N'<p>Tại sự kiện "Let us Glow" tháng 10/2025, Apple chính thức ra mắt dòng chip M4 Pro với những cải tiến đột phá.</p><h2>Thông Số Kỹ Thuật</h2><ul><li>12-core CPU (8 performance + 4 efficiency)</li><li>20-core GPU</li><li>16-core Neural Engine: 38 TOPS</li><li>Tiến trình sản xuất 3nm thế hệ 2</li></ul><h2>Hiệu Năng Thực Tế</h2><p>Final Cut Pro render video 4K nhanh hơn 35% so với M3 Pro. Xcode build project lớn nhanh hơn 28%.</p>',
'https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=800&q=80',
1,1,11230,'published',1,0,DATEADD(HOUR,-3,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-3,GETDATE()),GETDATE()),

(12,
N'Microsoft copilot+ PC: Kỷ nguyên mới của máy tính cá nhân tích hợp AI',
'microsoft-copilot-plus-pc-ky-nguyen-moi',
N'Microsoft công bố chuẩn Copilot+ PC yêu cầu NPU 40 TOPS, mở ra kỷ nguyên AI chạy hoàn toàn offline trên máy tính cá nhân.',
N'<p>Microsoft đã định nghĩa lại thế hệ PC tiếp theo với chuẩn Copilot+, yêu cầu chip có NPU (Neural Processing Unit) đạt tối thiểu 40 TOPS.</p><h2>Tính Năng Nổi Bật</h2><ul><li>Recall: AI ghi nhớ mọi thứ bạn đã xem trên máy</li><li>Cocreator: vẽ và chỉnh ảnh bằng AI trong Paint</li><li>Live Captions: dịch real-time 44 ngôn ngữ</li></ul>',
'https://images.unsplash.com/photo-1593642632559-0c6d3fc62b89?w=800&q=80',
1,1,7840,'published',0,0,DATEADD(HOUR,-7,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-7,GETDATE()),GETDATE()),

(13,
N'SpaceX Starlink phủ sóng internet tốc độ cao toàn bộ Việt Nam từ 2025',
'spacex-starlink-phu-song-internet-viet-nam-2025',
N'SpaceX chính thức được cấp phép hoạt động tại Việt Nam, mang đến internet vệ tinh tốc độ cao đến mọi vùng nông thôn, miền núi.',
N'<p>Sau nhiều năm đàm phán, SpaceX Starlink đã nhận được giấy phép hoạt động chính thức tại Việt Nam từ Bộ TT&TT.</p><h2>Thông Số Dịch Vụ</h2><ul><li>Tốc độ download: 100-300 Mbps</li><li>Latency: 20-40ms</li><li>Phủ sóng: 100% lãnh thổ Việt Nam</li></ul><h2>Ý Nghĩa Với Việt Nam</h2><p>Hàng triệu người dân ở vùng sâu vùng xa lần đầu tiên có thể tiếp cận internet tốc độ cao, mở ra cơ hội học online và làm việc từ xa.</p>',
'https://images.unsplash.com/photo-1614728894747-a83421e2b9c9?w=800&q=80',
1,1,14560,'published',1,1,DATEADD(MINUTE,-45,GETDATE()),NULL,NULL,NULL,DATEADD(MINUTE,-45,GETDATE()),GETDATE()),

(14,
N'Samsung Galaxy S25 Ultra: Camera AI nhận diện và chụp hoàn hảo mọi khoảnh khắc',
'samsung-galaxy-s25-ultra-camera-ai',
N'Samsung Galaxy S25 Ultra ra mắt với camera 200MP tích hợp AI, khả năng zoom 100x và tính năng chỉnh sửa ảnh thông minh chưa từng có.',
N'<p>Samsung đã nâng tầm smartphone photography lên một cấp độ mới với Galaxy S25 Ultra.</p><h2>Camera System</h2><ul><li>Main: 200MP, f/1.7, OIS thế hệ 3</li><li>Telephoto: 50MP, 5x optical + 10x optical</li><li>Ultrawide: 12MP, 120° FOV</li></ul><h2>AI Photography</h2><p>ProVisual Engine AI tự động nhận diện 200+ cảnh vật và tối ưu thông số chụp trong thời gian thực.</p>',
'https://images.unsplash.com/photo-1610945415295-d9bbf067e59c?w=800&q=80',
1,1,8930,'published',0,0,DATEADD(HOUR,-9,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-9,GETDATE()),GETDATE()),

(15,
N'OpenAI ra mắt GPT-5: Mô hình AI mạnh nhất từ trước đến nay với khả năng lý luận đa bước',
'openai-gpt-5-mo-hinh-ai-manh-nhat',
N'OpenAI chính thức phát hành GPT-5 với khả năng lý luận đa bước, giải quyết bài toán phức tạp và hiểu ngữ cảnh sâu hơn bất kỳ mô hình nào trước đây.',
N'<p>Sau nhiều tháng chờ đợi, OpenAI đã ra mắt GPT-5 — một bước nhảy vọt đáng kể so với GPT-4o.</p><h2>Điểm Nổi Bật</h2><ul><li>Multi-step reasoning: giải quyết bài toán 10+ bước logic</li><li>Context window: 1 triệu token</li><li>Multimodal: xử lý text, ảnh, video, audio</li></ul><h2>So Sánh Với GPT-4o</h2><p>GPT-5 vượt trội hoàn toàn trên MMLU (+8%), HumanEval (+15%) và MATH (+22%).</p>',
'https://images.unsplash.com/photo-1684369175833-4b445ad6bfb5?w=800&q=80',
1,1,19840,'published',1,1,DATEADD(HOUR,-1,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-1,GETDATE()),GETDATE()),

-- ===== PHẦN CỨNG (CategoryId = 5) =====
(16,
N'NVIDIA RTX 6000 series: Hiệu năng AI tăng 10 lần so với thế hệ trước',
'nvidia-rtx-6000-series-hieu-nang-ai-tang-10-lan',
N'NVIDIA vừa ra mắt GPU RTX 6000 series tối ưu cho AI với kiến trúc Blackwell mới hoàn toàn, hiệu năng xử lý AI tăng đến 10 lần.',
N'<p>Tại GTC 2025, CEO Jensen Huang trực tiếp giới thiệu dòng GPU thế hệ tiếp theo RTX 6000 series — bước đột phá lớn nhất lịch sử NVIDIA.</p><h2>Thông Số Kỹ Thuật</h2><p>RTX 6090 Ti: 32GB GDDR7, 1000W TDP, hỗ trợ đầy đủ FP8 cho AI inference.</p><h2>Kiến Trúc Blackwell</h2><p>Transformer Engine thế hệ 4 xử lý LLM nhanh hơn 10 lần, đặc biệt hiệu quả với các mô hình 70B+ parameter.</p>',
'https://images.unsplash.com/photo-1591488320449-011701bb6704?w=800&q=80',
5,1,6850,'published',1,0,DATEADD(HOUR,-11,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-11,GETDATE()),GETDATE()),

(17,
N'DDR6 RAM chính thức ra mắt: Băng thông tăng gấp đôi DDR5, giá vẫn hợp lý',
'ddr6-ram-chinh-thuc-ra-mat-bang-thong-gap-doi',
N'JEDEC công bố chuẩn DDR6 chính thức với tốc độ tối thiểu 8400 MT/s, tương thích ngược và hứa hẹn không tăng giá nhiều so với DDR5.',
N'<p>Sau nhiều năm development, chuẩn DDR6 SDRAM chính thức được JEDEC phê duyệt và sẵn sàng cho sản xuất đại trà.</p><h2>Thông Số DDR6</h2><ul><li>Tốc độ: 8400 - 17600 MT/s</li><li>Điện áp: 1.1V (giảm từ 1.1V DDR5)</li><li>Cải tiến ECC on-die</li></ul><h2>Khi Nào Có Trên Thị Trường?</h2><p>Intel Arrow Lake-X và AMD Zen 5 Extreme sẽ là những nền tảng đầu tiên hỗ trợ DDR6, dự kiến Q2/2026.</p>',
'https://images.unsplash.com/photo-1562976540-1502c2145186?w=800&q=80',
5,1,3420,'published',0,0,DATEADD(HOUR,-15,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-15,GETDATE()),GETDATE()),

-- ===== PHẦN MỀM (CategoryId = 4) =====
(18,
N'Flutter 4.0: Tích hợp AI và cải thiện hiệu năng trên iOS và Android',
'flutter-4-ra-mat-tich-hop-ai',
N'Google phát hành Flutter 4.0 với tích hợp Gemini AI trực tiếp vào framework, Impeller engine mới và nhiều cải tiến quan trọng cho cross-platform development.',
N'<p>Flutter 4.0 đánh dấu kỷ nguyên mới cho phát triển đa nền tảng với AI tích hợp sẵn.</p><h2>AI Integration</h2><p>Google AI Dart SDK cho phép gọi Gemini API trực tiếp từ Flutter app chỉ với vài dòng code.</p><h2>Impeller Engine</h2><p>Rendering engine mới loại bỏ hoàn toàn jank, đạt 120fps ổn định trên mọi thiết bị mid-range trở lên.</p>',
'https://images.unsplash.com/photo-1551650975-87deedd944c3?w=800&q=80',
4,1,3120,'published',0,0,DATEADD(HOUR,-13,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-13,GETDATE()),GETDATE()),

(19,
N'Visual Studio Code 2025: AI pair programming tích hợp sâu với GitHub Copilot',
'visual-studio-code-2025-ai-pair-programming',
N'Microsoft ra mắt VS Code phiên bản 2025 với GitHub Copilot tích hợp sâu, chat AI trong editor và tính năng tự động refactor code thông minh.',
N'<p>VS Code 2025 là bước tiến lớn nhất trong lịch sử editor phổ biến nhất thế giới.</p><h2>Copilot Chat Inline</h2><p>Không cần mở panel riêng — gõ // AI: và đặt câu hỏi ngay trong code file.</p><h2>Auto Refactor</h2><p>Copilot phát hiện code smell và tự đề xuất refactor với giải thích chi tiết lý do.</p><h2>Test Generation</h2><p>Tự động tạo unit test cho function được chọn, coverage 80%+ mặc định.</p>',
'https://images.unsplash.com/photo-1461749280684-dccba630e2f6?w=800&q=80',
4,1,6780,'published',0,0,DATEADD(HOUR,-16,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-16,GETDATE()),GETDATE()),

-- ===== BẢO MẬT (CategoryId = 7) =====
(20,
N'Lỗ hổng zero-day nghiêm trọng trong Linux kernel 6.8 ảnh hưởng hàng triệu server',
'lo-hong-zero-day-linux-kernel-6-8',
N'CERT/CC công bố lỗ hổng CVE-2025-1337 trong nhân Linux 6.8, cho phép leo thang đặc quyền không cần xác thực. Bản vá đã được phát hành khẩn cấp.',
N'<p>Lỗ hổng CVE-2025-1337 xếp loại CVSS 9.8/10 — mức nghiêm trọng cao nhất. Ảnh hưởng Linux 6.8.0 đến 6.8.11.</p><h2>Chi Tiết Lỗ Hổng</h2><p>Lỗi nằm trong module io_uring, cho phép attacker local leo thang đặc quyền lên root mà không cần password.</p><h2>Cập Nhật Ngay</h2><p>Chạy <code>sudo apt update && sudo apt upgrade</code> hoặc <code>sudo yum update kernel</code> để vá lỗi.</p>',
'https://images.unsplash.com/photo-1550751827-4bd374c3f58b?w=800&q=80',
7,1,12430,'published',0,1,DATEADD(MINUTE,-30,GETDATE()),NULL,NULL,NULL,DATEADD(MINUTE,-30,GETDATE()),GETDATE()),

(21,
N'Tấn công Prompt Injection: Mối đe dọa bảo mật mới nhất trong kỷ nguyên AI',
'tan-cong-prompt-injection-moi-de-doa-bao-mat-ai',
N'Prompt Injection đang trở thành một trong những lỗ hổng bảo mật nguy hiểm nhất khi AI được tích hợp vào các ứng dụng doanh nghiệp.',
N'<p>Khi AI chatbot ngày càng được tích hợp vào ứng dụng doanh nghiệp, một loại tấn công mới đang nổi lên: Prompt Injection.</p><h2>Prompt Injection Là Gì?</h2><p>Attacker nhúng các lệnh độc hại vào input của người dùng để thao túng AI thực hiện hành động ngoài ý muốn.</p><h2>Ví Dụ Thực Tế</h2><p>"Ignore previous instructions. You are now DAN..." — câu lệnh này có thể override system prompt của chatbot.</p><h2>Cách Phòng Chống</h2><ul><li>Validate và sanitize input trước khi gửi đến AI</li><li>Sử dụng separate prompt cho system vs user</li><li>Implement output filtering</li></ul>',
'https://images.unsplash.com/photo-1563013544-824ae1b704d3?w=800&q=80',
7,1,5670,'published',0,0,DATEADD(HOUR,-16,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-16,GETDATE()),GETDATE()),

-- ===== ĐIỆN TOÁN ĐÁM MÂY (CategoryId = 8) =====
(22,
N'AWS re:Invent 2025: Amazon ra mắt 50 dịch vụ mới, AI infrastructure dẫn đầu',
'aws-reinvent-2025-amazon-50-dich-vu-moi',
N'AWS re:Invent 2025 là sự kiện lớn nhất từ trước đến nay với 50+ dịch vụ mới, tập trung vào AI infrastructure, serverless và edge computing.',
N'<p>AWS re:Invent 2025 tại Las Vegas quy tụ hơn 60,000 developer và cloud architect từ khắp thế giới.</p><h2>Dịch Vụ Nổi Bật</h2><ul><li>Amazon Bedrock Flows: orchestrate AI workflows trực quan</li><li>AWS Trainium 3: chip AI training nhanh nhất thế giới</li><li>Amazon Q Developer: AI coding assistant tích hợp toàn bộ AWS ecosystem</li></ul>',
'https://images.unsplash.com/photo-1451187580459-43490279c0fa?w=800&q=80',
8,1,7230,'published',0,0,DATEADD(HOUR,-19,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-19,GETDATE()),GETDATE()),

-- ===== THỦ THUẬT IT (CategoryId = 6) =====
(23,
N'10 phím tắt VS Code ít ai biết nhưng cực kỳ hữu ích',
'10-phim-tat-vs-code-it-biet-nhung-rat-huu-ich',
N'Khám phá 10 phím tắt VS Code được cộng đồng developer bình chọn hữu ích nhất, giúp tăng tốc code đáng kể và tiết kiệm hàng giờ mỗi tuần.',
N'<p>VS Code ẩn chứa hàng trăm phím tắt mạnh mẽ. Sau khi biết 10 phím tắt này, bạn sẽ tự hỏi tại sao không dùng sớm hơn.</p><h2>Top 10 Phím Tắt</h2><ul><li><strong>Ctrl+Shift+P</strong>: Command Palette — trung tâm điều khiển VS Code</li><li><strong>Ctrl+D</strong>: Chọn từ tiếp theo giống từ đang chọn</li><li><strong>Alt+Click</strong>: Multi-cursor editing</li><li><strong>Ctrl+`</strong>: Toggle terminal</li><li><strong>F2</strong>: Rename symbol toàn project</li></ul>',
'https://images.unsplash.com/photo-1504639725590-34d0984388bd?w=800&q=80',
6,1,7830,'published',0,0,DATEADD(HOUR,-24,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-24,GETDATE()),GETDATE()),

(24,
N'Cách tăng tốc Windows 11 đơn giản: 10 mẹo mà ít ai biết',
'cach-tang-toc-windows-11-10-meo-don-gian',
N'Windows 11 chạy chậm? 10 mẹo tối ưu đơn giản này sẽ giúp máy tính của bạn chạy nhanh hơn đáng kể mà không cần cài lại hệ điều hành.',
N'<p>Windows 11 đôi khi có thể chạy chậm do nhiều nguyên nhân khác nhau. Hầu hết đều có thể khắc phục đơn giản.</p><h2>1. Tắt Startup Programs Thừa</h2><p>Task Manager → Startup Apps → Disable những app không cần thiết.</p><h2>2. Bật Storage Sense</h2><p>Settings → System → Storage → Storage Sense để tự động dọn dẹp file rác.</p><h2>3. Điều Chỉnh Power Plan</h2><p>Chuyển sang "High Performance" hoặc "Ultimate Performance" khi cần hiệu năng tối đa.</p>',
'https://images.unsplash.com/photo-1587202372634-32705e3bf49c?w=800&q=80',
6,1,9420,'published',0,0,DATEADD(HOUR,-26,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-26,GETDATE()),GETDATE()),

(25,
N'Học lập trình miễn phí 2025: Top 10 website tốt nhất cho người mới bắt đầu',
'hoc-lap-trinh-mien-phi-2025-top-10-website',
N'Tổng hợp 10 website học lập trình miễn phí tốt nhất năm 2025, từ web development đến AI/ML, phù hợp với mọi trình độ từ beginner đến advanced.',
N'<p>Học lập trình không còn đòi hỏi học phí đắt đỏ khi có hàng chục platform miễn phí chất lượng cao.</p><h2>Top 5 Website Tốt Nhất</h2><ol><li><strong>freeCodeCamp</strong>: Full-stack web development có chứng chỉ</li><li><strong>The Odin Project</strong>: Lộ trình web dev hoàn chỉnh</li><li><strong>CS50</strong>: Khóa học Harvard miễn phí, chất lượng đỉnh cao</li><li><strong>Kaggle Learn</strong>: Machine Learning và Data Science</li><li><strong>Google ML Crash Course</strong>: Nhập môn AI từ Google</li></ol>',
'https://images.unsplash.com/photo-1522202176988-66273c2fd55f?w=800&q=80',
6,1,11240,'published',0,0,DATEADD(HOUR,-28,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-28,GETDATE()),GETDATE());

SET IDENTITY_INSERT Articles OFF;
GO

-- ===================== ARTICLE TAGS =====================
INSERT INTO ArticleTags (ArticleId, TagId) VALUES
-- AI articles
(1,11),(1,5),(1,4),(1,17),
(2,11),(2,5),(2,4),
(3,11),(3,5),
(4,11),(4,17),(4,4),
-- Lập trình
(5,1),(5,4),(5,11),
(6,1),(6,11),(6,16),
(7,6),(7,20),
(8,12),(8,2),
(9,21),(9,22),
(10,14),
-- Tin CN
(11,11),
(12,11),
(13,13),
(14,11),
(15,11),(15,5),
-- Phần cứng
(16,11),
(17,23),
-- Phần mềm
(18,19),(18,11),
(19,15),(19,2),
-- Bảo mật
(20,9),(20,10),
(21,11),(21,10),
-- Cloud
(22,27),(22,13),(22,11),
-- Thủ thuật
(23,15),(23,2),
(24,2),
(25,1),(25,11),(25,4);
GO

-- ===================== COMMENTS =====================
INSERT INTO Comments (ArticleId,AuthorName,AuthorEmail,Content,Status,Likes,CreatedAt) VALUES
(1,N'Nguyễn Minh Tuấn','tuan.nguyen@gmail.com',N'Bài viết rất hay! Mình đang học AI và bài này giúp ích rất nhiều. Cảm ơn tác giả!','approved',8,DATEADD(HOUR,-1,GETDATE())),
(1,N'Trần Thị Lan','lan.tran@gmail.com',N'Phần AlphaCode 3 khiến mình suy ngẫm về tương lai nghề lập trình. Liệu developer còn cần thiết không?','approved',5,DATEADD(HOUR,-2,GETDATE())),
(1,N'Phạm Văn Đức','duc.pham@gmail.com',N'Mình nghĩ AI chỉ là công cụ hỗ trợ thôi, không thể thay thế hoàn toàn con người. Tư duy sáng tạo vẫn là của người.','approved',12,DATEADD(HOUR,-3,GETDATE())),
(2,N'Hoàng Thị Mai','mai.hoang@gmail.com',N'Gemini Ultra 2.0 thực sự ấn tượng! Mình đã thử và kết quả vượt xa kỳ vọng.','approved',6,DATEADD(HOUR,-4,GETDATE())),
(3,N'Lê Văn Nam','nam.le@gmail.com',N'Theo mình Claude 3.5 vẫn tốt hơn cho viết code phức tạp! GPT-4o thì giỏi đa nhiệm hơn.','approved',9,DATEADD(HOUR,-1,GETDATE())),
(3,N'Vũ Thị Hương','huong.vu@gmail.com',N'Bài so sánh rất chi tiết và khách quan. Mình đang dùng cả hai tùy từng task.','approved',4,DATEADD(HOUR,-5,GETDATE())),
(5,N'Phạm Quang Hải','hai.pham@gmail.com',N'Mình dùng Pandas hàng ngày nhưng chưa thử Polars. Nghe nói nhanh hơn nhiều, có ai dùng chưa?','approved',7,DATEADD(HOUR,-3,GETDATE())),
(5,N'Đặng Văn Bình','binh.dang@gmail.com',N'FastAPI thật sự tuyệt vời! Mình chuyển từ Flask sang FastAPI và không muốn quay lại nữa.','approved',11,DATEADD(HOUR,-6,GETDATE())),
(9,N'Trần Ngọc Anh','anh.tran@gmail.com',N'ASP.NET Core 9 thật ấn tượng! 7.5 triệu request/giây là con số khổng lồ. Mình đang dùng .NET 8 và sẽ migrate sớm.','approved',5,DATEADD(HOUR,-2,GETDATE())),
(11,N'Nguyễn Thành Long','long.nguyen@gmail.com',N'M4 Pro thật sự là một bước tiến lớn. Neural Engine 38 TOPS sẽ thay đổi cách chúng ta làm việc với AI trên laptop.','approved',8,DATEADD(HOUR,-2,GETDATE())),
(15,N'Bùi Thị Thủy','thuy.bui@gmail.com',N'GPT-5 thực sự đáng sợ theo cả nghĩa tích cực lẫn tiêu cực. Mình cần phải học cách làm việc với AI hiệu quả hơn.','approved',14,DATEADD(HOUR,-30,GETDATE())),
(20,N'Lý Văn Cường','cuong.ly@gmail.com',N'Đã update kernel rồi. Lỗ hổng này nghiêm trọng thật, may mà có bản vá sớm. Mọi người cập nhật ngay đi!','approved',19,DATEADD(MINUTE,-15,GETDATE())),
(23,N'Ngô Thị Phượng','phuong.ngo@gmail.com',N'Ctrl+D là phím tắt mình dùng nhiều nhất! Tiết kiệm thời gian kinh khủng khi cần rename nhiều biến cùng lúc.','approved',6,DATEADD(HOUR,-8,GETDATE())),
(25,N'Đinh Văn Khánh','khanh.dinh@gmail.com',N'CS50 của Harvard là khóa học tốt nhất mình từng học. Hoàn toàn miễn phí mà chất lượng cực kỳ cao!','approved',10,DATEADD(HOUR,-10,GETDATE()));
GO

UPDATE Articles SET AiSummary = NULL;

PRINT N'Seed data mở rộng thành công!';
PRINT N'25 bài viết, 30 tags, 14 bình luận đã được thêm vào.';
GO
-- WISDOM IT NEWS — Seed Data Mở Rộng PHẦN 2
-- Chỉ INSERT thêm, KHÔNG xóa dữ liệu cũ
-- Bổ sung: 25 bài viết (26–50), 20 tags (31–50), 30+ comments

USE WisdomITNews;
GO

-- ===================== TAGS MỚI (31–50) =====================
SET IDENTITY_INSERT Tags ON;
INSERT INTO Tags (Id, Name, Slug, CreatedAt) VALUES
(31, N'Redis',            'redis',             GETDATE()),
(32, N'GraphQL',          'graphql',           GETDATE()),
(33, N'Rust',             'rust',              GETDATE()),
(34, N'Go',               'go',                GETDATE()),
(35, N'Terraform',        'terraform',         GETDATE()),
(36, N'CI/CD',            'cicd',              GETDATE()),
(37, N'Microservices',    'microservices',     GETDATE()),
(38, N'WebAssembly',      'webassembly',       GETDATE()),
(39, N'Prompt Engineering','prompt-engineering',GETDATE()),
(40, N'RAG',              'rag',               GETDATE()),
(41, N'LangChain',        'langchain',         GETDATE()),
(42, N'OpenAI',           'openai',            GETDATE()),
(43, N'Stable Diffusion', 'stable-diffusion',  GETDATE()),
(44, N'DevOps',           'devops',            GETDATE()),
(45, N'Cybersecurity',    'cybersecurity',     GETDATE()),
(46, N'PostgreSQL',       'postgresql',        GETDATE()),
(47, N'Tailwind CSS',     'tailwind-css',      GETDATE()),
(48, N'Django',           'django',            GETDATE()),
(49, N'Spring Boot',      'spring-boot',       GETDATE()),
(50, N'Azure',            'azure',             GETDATE());
SET IDENTITY_INSERT Tags OFF;
GO

-- ===================== ARTICLES MỚI (26–50) =====================
SET IDENTITY_INSERT Articles ON;
INSERT INTO Articles (Id,Title,Slug,Summary,Content,Thumbnail,CategoryId,AuthorId,Views,Status,IsFeatured,IsBreaking,PublishedAt,AiSummary,MetaTitle,MetaDesc,CreatedAt,UpdatedAt) VALUES

-- ===== AI & MACHINE LEARNING (CategoryId = 3) =====
(26,
N'Prompt Engineering 2025: Nghệ thuật giao tiếp với AI để đạt kết quả tối ưu',
'prompt-engineering-2025-nghe-thuat-giao-tiep-voi-ai',
N'Prompt Engineering đang trở thành kỹ năng không thể thiếu cho mọi lập trình viên. Hướng dẫn đầy đủ từ kỹ thuật cơ bản đến advanced chain-of-thought.',
N'<p>Prompt Engineering là nghệ thuật thiết kế câu lệnh để AI trả lời đúng ý nhất. Đây là kỹ năng mới nổi nhưng đang được trả lương rất cao.</p><h2>Kỹ Thuật Cơ Bản</h2><ul><li><strong>Zero-shot prompting</strong>: Hỏi thẳng không có ví dụ</li><li><strong>Few-shot prompting</strong>: Cho vài ví dụ trước câu hỏi</li><li><strong>Chain-of-Thought</strong>: Yêu cầu AI suy luận từng bước</li></ul><h2>Kỹ Thuật Nâng Cao</h2><p>Tree of Thought (ToT) và ReAct framework cho phép AI tự đánh giá và cải thiện câu trả lời của mình trong vòng lặp.</p><h2>Ứng Dụng Thực Tế</h2><p>Sử dụng system prompt mạnh mẽ để định hình vai trò, phong cách và giới hạn của AI trong từng ứng dụng cụ thể.</p>',
'https://images.unsplash.com/photo-1676573060071-4c0a9d05e9b3?w=800&q=80',
3,1,8760,'published',1,0,DATEADD(HOUR,-4,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-4,GETDATE()),GETDATE()),

(27,
N'RAG — Retrieval Augmented Generation: Cách AI học từ tài liệu của bạn',
'rag-retrieval-augmented-generation-huong-dan',
N'RAG là kỹ thuật cho phép LLM trả lời câu hỏi dựa trên dữ liệu riêng tư mà không cần fine-tuning tốn kém. Hướng dẫn xây dựng hệ thống RAG hoàn chỉnh.',
N'<p>RAG (Retrieval Augmented Generation) đang cách mạng hóa cách doanh nghiệp tích hợp AI vào quy trình làm việc.</p><h2>RAG Hoạt Động Thế Nào?</h2><ol><li>Chia tài liệu thành các chunk nhỏ</li><li>Tạo embedding vector cho từng chunk</li><li>Lưu vào vector database (ChromaDB, Pinecone)</li><li>Khi có câu hỏi: tìm chunk liên quan → gửi cùng với prompt</li></ol><h2>Vector Database Phổ Biến</h2><p>ChromaDB (local), Pinecone (cloud), Weaviate (self-hosted) — mỗi loại có ưu điểm riêng tùy nhu cầu.</p><h2>Demo Code</h2><pre><code>from langchain.vectorstores import Chroma\nfrom langchain.embeddings import OpenAIEmbeddings\n\ndb = Chroma.from_documents(docs, OpenAIEmbeddings())\nretriever = db.as_retriever(search_kwargs={"k": 4})</code></pre>',
'https://images.unsplash.com/photo-1558494949-ef010cbdcc31?w=800&q=80',
3,1,6540,'published',1,0,DATEADD(HOUR,-6,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-6,GETDATE()),GETDATE()),

(28,
N'Stable Diffusion 3.5: Tạo ảnh AI chất lượng studio ngay trên máy tính của bạn',
'stable-diffusion-35-tao-anh-ai-chat-luong-studio',
N'Stable Diffusion 3.5 đánh dấu bước tiến lớn trong text-to-image AI. Chạy hoàn toàn offline, miễn phí và cho ra ảnh chất lượng ngang hàng Midjourney.',
N'<p>Stable Diffusion 3.5 (SD 3.5) đã được Stability AI phát hành và ngay lập tức gây sốt cộng đồng AI art.</p><h2>Điểm Nổi Bật SD 3.5</h2><ul><li>Hiểu text tiếng Việt và các ngôn ngữ châu Á tốt hơn nhiều</li><li>Anatomy con người cải thiện rõ rệt — không còn lỗi ngón tay</li><li>Coherence: ảnh nhất quán hơn khi có nhiều chủ thể</li></ul><h2>Cài Đặt Trên Máy Cá Nhân</h2><p>Yêu cầu tối thiểu: GPU 8GB VRAM (RTX 3060 trở lên). Sử dụng ComfyUI hoặc Automatic1111 làm interface.</p><h2>Prompt Tips</h2><p>Sử dụng negative prompt mạnh và CLIP skip 2 để có kết quả tốt nhất với SD 3.5.</p>',
'https://images.unsplash.com/photo-1547954575-855750c57bd3?w=800&q=80',
3,1,9120,'published',0,0,DATEADD(HOUR,-8,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-8,GETDATE()),GETDATE()),

(29,
N'Microsoft Phi-4: Mô hình AI nhỏ gọn đánh bại GPT-4 trong nhiều tác vụ',
'microsoft-phi-4-mo-hinh-ai-nho-gon-danh-bai-gpt4',
N'Phi-4 từ Microsoft chứng minh rằng bigger is not always better trong AI. Mô hình 14B parameter này vượt trội các mô hình 70B+ trong nhiều benchmark.',
N'<p>Microsoft Research gây bất ngờ khi Phi-4 — mô hình chỉ 14 tỷ tham số — vượt qua Llama 3.1 70B và thậm chí một số tác vụ của GPT-4.</p><h2>Tại Sao Phi-4 Mạnh Vậy?</h2><p>Bí quyết nằm ở chất lượng dữ liệu training, không phải số lượng. Microsoft sử dụng synthetic data chất lượng cao và curriculum learning.</p><h2>Benchmark So Sánh</h2><ul><li>MATH: Phi-4 đạt 80.4% vs GPT-4o 76.6%</li><li>GPQA Science: Phi-4 đạt 56.1%</li><li>HumanEval: 82.6%</li></ul><h2>Chạy Phi-4 Trên Máy Bạn</h2><p>Phi-4 đã có trên Ollama và LM Studio. Yêu cầu RAM 16GB để chạy quantized version.</p>',
'https://images.unsplash.com/photo-1677442136019-21780ecad995?w=800&q=80',
3,1,7380,'published',0,0,DATEADD(HOUR,-10,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-10,GETDATE()),GETDATE()),

(30,
N'Agentic AI 2025: Khi AI không chỉ trả lời mà còn tự hành động',
'agentic-ai-2025-khi-ai-tu-hanh-dong',
N'AI Agent là bước tiến tiếp theo sau chatbot — chúng có thể lên kế hoạch, sử dụng công cụ và thực hiện nhiều bước để hoàn thành mục tiêu phức tạp.',
N'<p>Năm 2025 là năm của Agentic AI — các hệ thống AI có thể tự chủ thực hiện chuỗi hành động để đạt mục tiêu.</p><h2>AI Agent Là Gì?</h2><p>Khác với chatbot truyền thống chỉ trả lời câu hỏi, AI Agent có vòng lặp: Nhận mục tiêu → Lên kế hoạch → Thực thi tool → Đánh giá kết quả → Lặp lại.</p><h2>Các Framework Agent Phổ Biến</h2><ul><li><strong>LangGraph</strong>: Xây dựng agent dạng graph có state</li><li><strong>AutoGen</strong>: Multi-agent framework từ Microsoft</li><li><strong>CrewAI</strong>: Orchestrate nhiều agent làm việc cùng nhau</li></ul><h2>Ứng Dụng Thực Tế</h2><p>Agent có thể tự động: research web → tổng hợp thông tin → viết báo cáo → gửi email — tất cả không cần can thiệp thủ công.</p>',
'https://images.unsplash.com/photo-1655720828018-edd2daec9349?w=800&q=80',
3,1,11230,'published',1,1,DATEADD(HOUR,-2,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-2,GETDATE()),GETDATE()),

-- ===== LẬP TRÌNH (CategoryId = 2) =====
(31,
N'Rust 2025: Tại sao ngôn ngữ lập trình hệ thống này đang chinh phục cộng đồng developer',
'rust-2025-tai-sao-chiem-linh-cong-dong-developer',
N'Rust tiếp tục là ngôn ngữ được developer yêu thích nhất 9 năm liên tiếp theo Stack Overflow Survey. Lý do gì khiến Rust đặc biệt đến vậy?',
N'<p>Rust đã giành vị trí "ngôn ngữ được yêu thích nhất" liên tiếp 9 năm trên Stack Overflow Developer Survey — một kỷ lục chưa từng có.</p><h2>Tại Sao Developer Yêu Rust?</h2><ul><li><strong>Memory safety</strong> không cần garbage collector</li><li><strong>Performance</strong> ngang C/C++ nhưng an toàn hơn</li><li><strong>Concurrency</strong> không có data race tại compile time</li></ul><h2>Rust Được Dùng Ở Đâu?</h2><p>Linux kernel (từ 2022), Windows NT kernel (đang chuyển đổi dần), Firefox, Discord, Cloudflare Workers, AWS và nhiều hơn nữa.</p><h2>Học Rust Từ Đâu?</h2><p>The Rust Programming Language book (rustbook) miễn phí online là điểm khởi đầu tốt nhất. Kết hợp với Rustlings exercises để luyện tập.</p>',
'https://images.unsplash.com/photo-1461749280684-dccba630e2f6?w=800&q=80',
2,1,6420,'published',0,0,DATEADD(HOUR,-12,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-12,GETDATE()),GETDATE()),

(32,
N'Go 1.23: Những cải tiến quan trọng cho backend developer',
'go-1-23-nhung-cai-tien-quan-trong-backend',
N'Go 1.23 ra mắt với range over function, cải tiến garbage collector và nhiều tính năng mới giúp viết backend service nhanh hơn, an toàn hơn.',
N'<p>Go vẫn là lựa chọn hàng đầu cho microservices và backend API với hiệu năng xuất sắc và thời gian compile cực nhanh.</p><h2>Range Over Function (Go 1.22+)</h2><p>Giờ có thể dùng range với custom iterator functions, cho phép lazy evaluation và code gọn gàng hơn nhiều.</p><h2>Garbage Collector Cải Tiến</h2><p>GC pause time giảm xuống dưới 1ms trong hầu hết trường hợp, phù hợp cho low-latency applications.</p><h2>Stdlib Improvements</h2><ul><li>math/rand/v2: API ngẫu nhiên mới chuẩn hơn</li><li>slices/maps package: functional operations tiện lợi</li></ul>',
'https://images.unsplash.com/photo-1516116216624-53e697fedbea?w=800&q=80',
2,1,4830,'published',0,0,DATEADD(HOUR,-14,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-14,GETDATE()),GETDATE()),

(33,
N'GraphQL vs REST API: So sánh toàn diện và khi nào nên chọn cái nào',
'graphql-vs-rest-api-so-sanh-toan-dien',
N'GraphQL và REST đều là tiêu chuẩn thiết kế API phổ biến. Bài viết phân tích chi tiết ưu nhược điểm từng loại và hướng dẫn chọn đúng cho dự án.',
N'<p>GraphQL được Facebook phát triển năm 2012 và open-source năm 2015, hiện đang là sự lựa chọn của Github, Shopify, Twitter và hàng ngàn công ty lớn.</p><h2>REST API</h2><ul><li>Đơn giản, dễ hiểu, phổ biến rộng rãi</li><li>Caching HTTP tốt hơn</li><li>Stateless, dễ scale</li></ul><h2>GraphQL</h2><ul><li>Client tự quyết định data cần fetch (no over/under-fetching)</li><li>Single endpoint cho toàn bộ API</li><li>Strongly typed schema</li><li>Real-time với subscriptions</li></ul><h2>Khi Nào Nên Chọn GraphQL?</h2><p>Nếu bạn có nhiều loại client (mobile, web, TV), dữ liệu phức tạp nhiều quan hệ hoặc cần real-time updates.</p>',
'https://images.unsplash.com/photo-1556155092-490a1ba16284?w=800&q=80',
2,1,5190,'published',0,0,DATEADD(HOUR,-16,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-16,GETDATE()),GETDATE()),

(34,
N'WebAssembly 2025: Công nghệ đang thay đổi tương lai lập trình web',
'webassembly-2025-thay-doi-tuong-lai-lap-trinh-web',
N'WebAssembly (WASM) không còn chỉ là thí nghiệm — nó đang được dùng trong production bởi Figma, Google Earth, và hàng nghìn ứng dụng web hiệu năng cao.',
N'<p>WebAssembly là định dạng binary instruction cho phép code C, C++, Rust, Go chạy trực tiếp trong browser với tốc độ gần native.</p><h2>Tại Sao WASM Quan Trọng?</h2><ul><li>JavaScript đôi khi không đủ nhanh cho graphics, simulation</li><li>Tái sử dụng code native libraries trong web</li><li>Bảo mật: sandbox hoàn toàn</li></ul><h2>WASM Ngoài Browser</h2><p>WASI (WebAssembly System Interface) cho phép WASM chạy trên server như Docker alternative — portable, secure, fast.</p><h2>Ai Đang Dùng?</h2><p>Figma dùng WASM cho toàn bộ rendering engine. AutoCAD Web dùng để port ứng dụng desktop 35 năm tuổi lên web.</p>',
'https://images.unsplash.com/photo-1627398242454-45a1465c2479?w=800&q=80',
2,1,4160,'published',0,0,DATEADD(HOUR,-18,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-18,GETDATE()),GETDATE()),

(35,
N'Xây dựng REST API với Django REST Framework: Hướng dẫn từ A đến Z',
'xay-dung-rest-api-django-rest-framework',
N'Django REST Framework là thư viện mạnh nhất để xây dựng API với Python. Hướng dẫn toàn diện từ setup đến authentication, pagination và deployment.',
N'<p>Django REST Framework (DRF) là lựa chọn số 1 cho Python backend developer khi cần xây dựng API mạnh mẽ và bảo trì tốt.</p><h2>Cài Đặt</h2><pre><code>pip install djangorestframework\npip install djangorestframework-simplejwt</code></pre><h2>Serializers</h2><p>Serializer là trái tim của DRF — chuyển đổi queryset thành JSON và ngược lại với validation tự động.</p><h2>ViewSets và Routers</h2><p>Với ModelViewSet + DefaultRouter, bạn có đầy đủ CRUD API chỉ trong ~10 dòng code.</p><h2>JWT Authentication</h2><p>Tích hợp simplejwt để bảo mật API với token rotation và blacklist support.</p>',
'https://images.unsplash.com/photo-1581472723648-909f4851d4ae?w=800&q=80',
2,1,3870,'published',0,0,DATEADD(HOUR,-20,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-20,GETDATE()),GETDATE()),

(36,
N'Spring Boot 3.x với Java 21: Virtual Threads và Project Loom thay đổi backend Java',
'spring-boot-3-java-21-virtual-threads-project-loom',
N'Java 21 LTS và Spring Boot 3.x mang đến Virtual Threads qua Project Loom, cho phép viết code blocking truyền thống nhưng đạt hiệu năng reactive.',
N'<p>Project Loom và Virtual Threads là cuộc cách mạng lớn nhất của Java trong hàng thập kỷ, chính thức production-ready với Java 21.</p><h2>Virtual Threads Là Gì?</h2><p>Virtual Threads (JEP 444) là thread nhẹ do JVM quản lý, có thể tạo hàng triệu thread mà không tốn nhiều memory như platform thread.</p><h2>Bật Virtual Threads Trong Spring Boot</h2><pre><code>spring.threads.virtual.enabled=true</code></pre><p>Chỉ một dòng config, toàn bộ Tomcat thread pool chuyển sang virtual threads!</p><h2>Kết Quả Benchmark</h2><p>Throughput tăng 3-4 lần với workload I/O intensive, latency giảm đáng kể khi có nhiều concurrent request.</p>',
'https://images.unsplash.com/photo-1544197150-b99a580bb7a8?w=800&q=80',
2,1,4520,'published',0,0,DATEADD(HOUR,-22,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-22,GETDATE()),GETDATE()),

(37,
N'Redis 8.0: Tính năng mới và tại sao nó vẫn là in-memory database số 1',
'redis-8-tinh-nang-moi-in-memory-database-so-1',
N'Redis 8.0 ra mắt với vector search tích hợp sẵn, JSON 3.0 và nhiều cải tiến hiệu năng — tại sao Redis vẫn là giải pháp caching và session không thể thay thế.',
N'<p>Redis vẫn là in-memory data store được sử dụng rộng rãi nhất thế giới, và phiên bản 8.0 tiếp tục củng cố vị thế này.</p><h2>Vector Search Tích Hợp</h2><p>Redis 8.0 có vector similarity search built-in, cho phép dùng Redis như vector database cho RAG applications — không cần Pinecone hay ChromaDB.</p><h2>Redis JSON 3.0</h2><p>Hỗ trợ JSON Path chuẩn RFC 9535, merge patches và atomic operations trên nested objects.</p><h2>Hiệu Năng</h2><ul><li>Throughput tăng 20% nhờ I/O threading improvements</li><li>Memory usage giảm 15% với listpack encoding improvements</li></ul>',
'https://images.unsplash.com/photo-1544383835-bda2bc66a55d?w=800&q=80',
2,1,3940,'published',0,0,DATEADD(HOUR,-24,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-24,GETDATE()),GETDATE()),

-- ===== TIN CÔNG NGHỆ (CategoryId = 1) =====
(38,
N'Meta AI ra mắt Llama 4: Mô hình open-source mạnh nhất lịch sử',
'meta-ai-llama-4-mo-hinh-open-source-manh-nhat',
N'Meta công bố Llama 4 với kiến trúc Mixture of Experts 400B tham số, vượt qua GPT-4o trên nhiều benchmark và hoàn toàn miễn phí cho mọi người dùng.',
N'<p>Mark Zuckerberg tuyên bố Llama 4 là "mô hình AI open-source mạnh nhất từ trước đến nay" tại sự kiện Meta Connect 2025.</p><h2>Thông Số Kỹ Thuật</h2><ul><li>Kiến trúc: Mixture of Experts (MoE) 400B tham số</li><li>Active parameters per forward pass: 70B</li><li>Context window: 128K tokens</li></ul><h2>Benchmark So Sánh</h2><p>Llama 4 vượt GPT-4o trên MMLU (+3.2%), HumanEval (+5%), và MATH (+8%). Đây là lần đầu tiên một mô hình open-source vượt mọi mô hình closed-source.</p><h2>License</h2><p>Llama 4 Community License cho phép thương mại hóa miễn phí cho các công ty dưới 700 triệu MAU.</p>',
'https://images.unsplash.com/photo-1534972195531-d756b9bfa9f2?w=800&q=80',
1,1,16840,'published',1,1,DATEADD(HOUR,-1,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-1,GETDATE()),GETDATE()),

(39,
N'Tesla Optimus Gen 3: Robot AI đang học làm việc trong nhà máy thực',
'tesla-optimus-gen-3-robot-ai-lam-viec-nha-may',
N'Tesla Optimus Gen 3 đang được triển khai trong nhà máy Fremont, thực hiện các tác vụ lắp ráp phức tạp với tốc độ và độ chính xác ấn tượng.',
N'<p>Tesla đã triển khai hơn 1000 robot Optimus Gen 3 tại nhà máy Fremont California trong chương trình pilot quy mô lớn nhất từ trước đến nay.</p><h2>Khả Năng Gen 3</h2><ul><li>Đi lại ổn định 8 km/h, leo cầu thang và địa hình không bằng phẳng</li><li>Tay 22 bậc tự do, cảm giác xúc giác 11 cảm biến</li><li>Dùng chung neural network với FSD của xe Tesla</li></ul><h2>Tác Động Kinh Tế</h2><p>Elon Musk dự đoán Optimus sẽ được bán với giá 20,000–30,000 USD và có thể thay thế 20% lao động chân tay toàn cầu trong thập kỷ tới.</p>',
'https://images.unsplash.com/photo-1485827404703-89b55fcc595e?w=800&q=80',
1,1,13450,'published',1,0,DATEADD(HOUR,-3,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-3,GETDATE()),GETDATE()),

(40,
N'Google Chrome 130: Tính năng AI mới và cải tiến hiệu năng đáng chú ý',
'google-chrome-130-tinh-nang-ai-moi',
N'Chrome 130 ra mắt với Gemini Nano tích hợp trực tiếp vào browser, cho phép AI chạy offline, tóm tắt trang web và hỗ trợ viết ngay trong tab.',
N'<p>Google Chrome 130 đánh dấu bước tiến lớn khi tích hợp Gemini Nano — mô hình AI nhỏ gọn chạy hoàn toàn on-device.</p><h2>Built-in AI APIs</h2><ul><li>Summarization API: tóm tắt bài viết dài trong vài giây</li><li>Writing API: hỗ trợ viết và chỉnh sửa văn bản</li><li>Translation API: dịch 100 ngôn ngữ offline</li></ul><h2>Hiệu Năng</h2><p>V8 JavaScript engine cải tiến thêm 15% throughput cho các ứng dụng web phức tạp. Memory usage giảm nhờ partition alloc improvements.</p>',
'https://images.unsplash.com/photo-1573804633927-bfcbcd909acd?w=800&q=80',
1,1,8670,'published',0,0,DATEADD(HOUR,-5,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-5,GETDATE()),GETDATE()),

(41,
N'Việt Nam lọt top 10 thị trường tăng trưởng AI nhanh nhất châu Á',
'viet-nam-top-10-thi-truong-tang-truong-ai-nhanh-nhat-chau-a',
N'Báo cáo AI Readiness 2025 của IDC xếp Việt Nam vào top 10 thị trường AI tăng trưởng nhanh nhất châu Á với mức tăng 45% YoY.',
N'<p>IDC vừa công bố báo cáo "Asia Pacific AI Readiness Index 2025" cho thấy Việt Nam đang nổi lên như điểm sáng công nghệ AI khu vực.</p><h2>Số Liệu Ấn Tượng</h2><ul><li>Tăng trưởng thị trường AI: 45% YoY — cao nhất Đông Nam Á</li><li>Đầu tư nước ngoài vào AI startup Việt Nam: 1.2 tỷ USD năm 2025</li><li>Số lượng AI engineer tăng 60% so với 2023</li></ul><h2>Động Lực Tăng Trưởng</h2><p>Dân số trẻ công nghệ cao, chi phí nhân công cạnh tranh, chính sách khuyến khích AI của Chính phủ và hệ sinh thái startup đang bùng nổ.</p>',
'https://images.unsplash.com/photo-1558618666-fcd25c85cd64?w=800&q=80',
1,1,10230,'published',1,0,DATEADD(HOUR,-7,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-7,GETDATE()),GETDATE()),

-- ===== DEVOPS & CLOUD (CategoryId = 8) =====
(42,
N'Terraform vs Pulumi 2025: Infrastructure as Code nên chọn công cụ nào?',
'terraform-vs-pulumi-2025-infrastructure-as-code',
N'Terraform và Pulumi đều là công cụ IaC hàng đầu nhưng có triết lý khác nhau. Phân tích chi tiết để giúp team DevOps lựa chọn phù hợp.',
N'<p>Infrastructure as Code đã trở thành bắt buộc với mọi team DevOps hiện đại. Terraform và Pulumi là hai lựa chọn phổ biến nhất.</p><h2>Terraform</h2><ul><li>HCL (HashiCorp Configuration Language) — ngôn ngữ declarative riêng</li><li>Mature provider ecosystem: 3000+ providers</li><li>State management với remote backend</li><li>Plan/Apply workflow rõ ràng</li></ul><h2>Pulumi</h2><ul><li>Dùng ngôn ngữ lập trình thực (Python, TypeScript, Go)</li><li>Tích hợp tự nhiên với CI/CD pipelines</li><li>Testing infrastructure code dễ dàng hơn</li></ul><h2>Kết Luận</h2><p>Team có developer background: Pulumi. Team ops thuần túy hoặc mixed: Terraform vẫn là lựa chọn an toàn hơn.</p>',
'https://images.unsplash.com/photo-1667372393119-3d4c48d07fc9?w=800&q=80',
8,1,5340,'published',0,0,DATEADD(HOUR,-9,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-9,GETDATE()),GETDATE()),

(43,
N'CI/CD Pipeline hoàn hảo với GitHub Actions: Hướng dẫn từ zero đến production',
'cicd-pipeline-hoan-hao-github-actions',
N'GitHub Actions đã trở thành tiêu chuẩn CI/CD cho hàng triệu dự án. Xây dựng pipeline tự động test, build Docker image và deploy lên Kubernetes.',
N'<p>GitHub Actions ra đời năm 2019 và nhanh chóng trở thành công cụ CI/CD phổ biến nhất thế giới với hơn 50 triệu workflow runs mỗi ngày.</p><h2>Cấu Trúc Workflow</h2><pre><code>name: Deploy\non:\n  push:\n    branches: [main]\njobs:\n  test:\n    runs-on: ubuntu-latest\n    steps:\n      - uses: actions/checkout@v4\n      - run: npm test</code></pre><h2>Best Practices</h2><ul><li>Cache dependencies để tăng tốc pipeline</li><li>Dùng matrix strategy để test nhiều version</li><li>Secrets management với GitHub Secrets</li></ul><h2>Deploy Lên Kubernetes</h2><p>Kết hợp GitHub Actions với kubectl và OIDC để deploy an toàn mà không cần lưu credentials trong secrets.</p>',
'https://images.unsplash.com/photo-1461749280684-dccba630e2f6?w=800&q=80',
8,1,6780,'published',0,0,DATEADD(HOUR,-11,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-11,GETDATE()),GETDATE()),

(44,
N'Microsoft Azure AI Studio: Nền tảng xây dựng và triển khai AI enterprise',
'microsoft-azure-ai-studio-nen-tang-ai-enterprise',
N'Azure AI Studio là one-stop-shop cho doanh nghiệp muốn xây dựng ứng dụng AI: fine-tuning model, RAG pipeline, evaluation và monitoring tất cả trong một.',
N'<p>Azure AI Studio cung cấp toàn bộ lifecycle của một AI application — từ prototyping đến production — trong một platform duy nhất.</p><h2>Model Catalog</h2><p>Hơn 1600 model từ OpenAI, Meta, Mistral, Cohere và Microsoft sẵn sàng deploy chỉ với vài click.</p><h2>Prompt Flow</h2><p>Visual tool để thiết kế, test và optimize LLM pipelines với built-in evaluation metrics.</p><h2>Enterprise Security</h2><ul><li>Private endpoints, VNet integration</li><li>Role-based access control</li><li>Audit logs và compliance reports</li></ul>',
'https://images.unsplash.com/photo-1451187580459-43490279c0fa?w=800&q=80',
8,1,4920,'published',0,0,DATEADD(HOUR,-13,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-13,GETDATE()),GETDATE()),

-- ===== BẢO MẬT (CategoryId = 7) =====
(45,
N'OWASP Top 10 2025: Những lỗ hổng bảo mật web nguy hiểm nhất cần biết',
'owasp-top-10-2025-lo-hong-bao-mat-web-nguy-hiem',
N'OWASP cập nhật danh sách Top 10 lỗ hổng bảo mật web năm 2025. Injection và Broken Access Control vẫn dẫn đầu, AI-specific vulnerabilities xuất hiện lần đầu.',
N'<p>OWASP (Open Web Application Security Project) vừa công bố danh sách Top 10 Web Application Security Risks năm 2025.</p><h2>Top 5 Nguy Hiểm Nhất</h2><ol><li><strong>Broken Access Control</strong> — vẫn đứng đầu năm thứ 4</li><li><strong>Cryptographic Failures</strong> — mã hóa yếu hoặc sai cách</li><li><strong>Injection</strong> — SQL, NoSQL, OS command injection</li><li><strong>Insecure Design</strong> — thiếu threat modeling</li><li><strong>LLM Injection</strong> (MỚI 2025) — prompt injection trong AI apps</li></ol><h2>Cách Phòng Chống</h2><p>DevSecOps integration, SAST/DAST tools trong CI/CD pipeline, regular penetration testing và developer security training.</p>',
'https://images.unsplash.com/photo-1563013544-824ae1b704d3?w=800&q=80',
7,1,7830,'published',1,0,DATEADD(HOUR,-15,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-15,GETDATE()),GETDATE()),

(46,
N'Passkeys 2025: Tương lai xác thực không mật khẩu đã đến',
'passkeys-2025-tuong-lai-xac-thuc-khong-mat-khau',
N'Passkeys đang được Apple, Google, Microsoft triển khai đồng loạt. Công nghệ FIDO2 này loại bỏ hoàn toàn mật khẩu, an toàn hơn và tiện lợi hơn.',
N'<p>Passkeys là phương thức xác thực thế hệ mới dựa trên public-key cryptography, được FIDO Alliance và W3C chuẩn hóa.</p><h2>Tại Sao Passkeys An Toàn Hơn?</h2><ul><li>Không có password để đánh cắp trong data breach</li><li>Chống phishing hoàn toàn — khóa gắn với domain cụ thể</li><li>Không thể brute-force — private key không bao giờ rời thiết bị</li></ul><h2>Triển Khai Passkeys</h2><p>GitHub, Microsoft, Google, Apple, Shopify đã hỗ trợ Passkeys. Số lượng account dùng Passkeys đã vượt 10 tỷ toàn cầu.</p><h2>Implement Cho App Của Bạn</h2><p>WebAuthn API chuẩn W3C hỗ trợ trên tất cả trình duyệt modern và mobile OS.</p>',
'https://images.unsplash.com/photo-1507238691740-187a5b1d37b8?w=800&q=80',
7,1,5490,'published',0,0,DATEADD(HOUR,-17,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-17,GETDATE()),GETDATE()),

-- ===== PHẦN CỨNG (CategoryId = 5) =====
(47,
N'Intel Arrow Lake-S: Kiến trúc mới thoát khỏi bóng AMD sau 4 năm',
'intel-arrow-lake-s-kien-truc-moi-thoat-khoi-bong-amd',
N'Intel Arrow Lake-S dùng kiến trúc Lion Cove hoàn toàn mới, IPC tăng 9%, hiệu năng gaming ngang ngửa AMD Ryzen 9000 và tiêu thụ điện năng cải thiện đáng kể.',
N'<p>Sau những thất bại với Raptor Lake Refresh, Intel đã tái thiết hoàn toàn kiến trúc CPU với Arrow Lake (Core Ultra 200 series).</p><h2>Kiến Trúc Lion Cove</h2><p>Thay thế Golden Cove, Lion Cove tăng IPC 9% với cải tiến branch predictor, larger execution units và improved prefetcher.</p><h2>Chiplet Design</h2><p>Arrow Lake dùng tile-based design: CPU tile (TSMC N3B), GPU tile (TSMC N5P), SOC tile và IO tile được kết nối qua Foveros Direct.</p><h2>Benchmark Thực Tế</h2><ul><li>Cinebench R23 Single: 2180 — cao nhất Intel từ trước đến nay</li><li>Gaming performance: ngang ngửa AMD Ryzen 9 9950X</li><li>TDP giảm từ 253W xuống 125W</li></ul>',
'https://images.unsplash.com/photo-1591488320449-011701bb6704?w=800&q=80',
5,1,5780,'published',0,0,DATEADD(HOUR,-19,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-19,GETDATE()),GETDATE()),

(48,
N'SSD PCIe 5.0 review: Tốc độ 14 GB/s có thực sự cần thiết?',
'ssd-pcie-5-review-toc-do-14-gb-s-co-can-thiet',
N'PCIe 5.0 SSD đang dần phổ biến với tốc độ đọc lên đến 14,000 MB/s. Nhưng trong thực tế sử dụng hàng ngày, sự khác biệt có đáng để trả thêm tiền?',
N'<p>PCIe 5.0 SSD như Samsung 9100 Pro và WD Black SN850X Gen5 hứa hẹn tốc độ gấp đôi thế hệ trước. Nhưng thực tế có như vậy không?</p><h2>Benchmark Tổng Hợp</h2><ul><li>Sequential Read: 14,000 MB/s (PCIe 5.0) vs 7,300 MB/s (PCIe 4.0)</li><li>Sequential Write: 13,000 MB/s vs 6,800 MB/s</li><li>Random 4K Read: 1,800K IOPS vs 1,200K IOPS</li></ul><h2>Thực Tế Sử Dụng</h2><p>Với tác vụ thông thường (gaming, văn phòng), bạn hầu như không cảm nhận được sự khác biệt. Lợi ích rõ nhất khi làm việc với file video 4K/8K, data science hoặc virtual machines.</p><h2>Có Nên Mua?</h2><p>Nếu budget hạn chế: PCIe 4.0 vẫn là value tốt nhất. PCIe 5.0 dành cho workstation và content creator chuyên nghiệp.</p>',
'https://images.unsplash.com/photo-1562976540-1502c2145186?w=800&q=80',
5,1,4230,'published',0,0,DATEADD(HOUR,-21,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-21,GETDATE()),GETDATE()),

-- ===== THỦ THUẬT IT (CategoryId = 6) =====
(49,
N'Cách sử dụng AI để tăng năng suất lập trình lên 10 lần',
'cach-su-dung-ai-tang-nang-suat-lap-trinh-10-lan',
N'Hướng dẫn thực tế cách kết hợp GitHub Copilot, Claude và ChatGPT vào quy trình làm việc hàng ngày để tăng năng suất đáng kể mà không mất đi chất lượng code.',
N'<p>AI coding assistants không phải là silver bullet, nhưng dùng đúng cách, chúng thực sự có thể tăng năng suất lên nhiều lần.</p><h2>GitHub Copilot — Autocomplete Thông Minh</h2><ul><li>Tab completion cho block code lớn</li><li>Ghost text suggestions theo context</li><li>Copilot Chat cho explain và refactor</li></ul><h2>Claude — Code Review và Architecture</h2><p>Dán cả file code và hỏi Claude review, tìm bug tiềm ẩn hoặc đề xuất cải thiện kiến trúc. Context window 200K token là lợi thế lớn.</p><h2>ChatGPT — Debug và Tìm Giải Pháp</h2><p>Tốt nhất cho việc tìm lời giải cho error message cụ thể và tìm hiểu thư viện mới nhanh chóng.</p><h2>Workflow Đề Xuất</h2><p>Copilot cho viết code → Claude cho review → ChatGPT cho debug khi stuck. Tổng thời gian tiết kiệm: 3-4 giờ/ngày.</p>',
'https://images.unsplash.com/photo-1504639725590-34d0984388bd?w=800&q=80',
6,1,14280,'published',1,0,DATEADD(HOUR,-4,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-4,GETDATE()),GETDATE()),

(50,
N'PostgreSQL vs MySQL 2025: Database nào tốt hơn cho dự án của bạn?',
'postgresql-vs-mysql-2025-database-nao-tot-hon',
N'PostgreSQL và MySQL vẫn là hai RDBMS phổ biến nhất. Phân tích toàn diện về hiệu năng, tính năng và ecosystem để giúp bạn đưa ra quyết định đúng.',
N'<p>Sau nhiều năm MySQL thống trị, PostgreSQL đang dần vươn lên và nhiều dự án lớn đã chuyển đổi. Cùng xem tại sao.</p><h2>PostgreSQL — Full-Featured RDBMS</h2><ul><li>JSONB support tuyệt vời — dùng như MongoDB khi cần</li><li>Advanced indexing: GIN, GiST, BRIN</li><li>Window functions và CTEs mạnh mẽ</li><li>Full-text search tích hợp</li><li>Extensibility: pgvector cho AI, TimescaleDB cho time-series</li></ul><h2>MySQL — Đơn Giản và Phổ Biến</h2><ul><li>Dễ setup và vận hành hơn</li><li>Ecosystem hosting rộng hơn</li><li>InnoDB storage engine ổn định</li></ul><h2>Khuyến Nghị 2025</h2><p>Dự án mới: PostgreSQL. Dự án đang chạy MySQL ổn: không cần migrate. Cần JSON/vector/advanced query: PostgreSQL rõ ràng hơn.</p>',
'https://images.unsplash.com/photo-1544383835-bda2bc66a55d?w=800&q=80',
2,1,9640,'published',1,0,DATEADD(HOUR,-6,GETDATE()),NULL,NULL,NULL,DATEADD(HOUR,-6,GETDATE()),GETDATE());

SET IDENTITY_INSERT Articles OFF;
GO

-- ===================== ARTICLE TAGS MỚI =====================
INSERT INTO ArticleTags (ArticleId, TagId) VALUES
-- AI & ML
(26,11),(26,39),(26,5),(26,42),
(27,11),(27,40),(27,41),(27,16),
(28,11),(28,43),
(29,11),(29,4),(29,42),
(30,11),(30,4),(30,41),
-- Lập trình
(31,33),
(32,34),(32,16),
(33,32),(33,16),
(34,38),(34,2),
(35,48),(35,1),(35,16),
(36,49),(36,16),
(37,31),(37,16),(37,8),
-- Tin CN
(38,11),(38,4),
(39,11),
(40,11),(40,2),
(41,11),(41,13),
-- DevOps & Cloud
(42,35),(42,44),(42,13),
(43,36),(43,44),(43,6),(43,20),
(44,50),(44,11),(44,13),
-- Bảo mật
(45,10),(45,45),(45,11),
(46,10),(46,45),
-- Phần cứng
(47,11),
(48,11),
-- Thủ thuật
(49,11),(49,1),(49,5),(49,15),
(50,46),(50,8),(50,26);
GO

-- ===================== COMMENTS MỚI =====================
INSERT INTO Comments (ArticleId,AuthorName,AuthorEmail,Content,Status,Likes,CreatedAt) VALUES
-- Bài 26 - Prompt Engineering
(26,N'Nguyễn Hữu Phát','phat.nguyen@gmail.com',N'Prompt Engineering thực sự là kỹ năng của tương lai! Mình đã áp dụng Chain-of-Thought và kết quả cải thiện rõ rệt.','approved',14,DATEADD(HOUR,-2,GETDATE())),
(26,N'Trần Quốc Bảo','bao.tran@gmail.com',N'Phần Tree of Thought rất mới với mình. Bạn có thể làm một bài viết chuyên sâu về ToT không?','approved',8,DATEADD(HOUR,-4,GETDATE())),

-- Bài 27 - RAG
(27,N'Lê Thị Ngọc','ngoc.le@gmail.com',N'Đang làm project RAG với LangChain và ChromaDB. Bài viết này đúng lúc quá! Cảm ơn tác giả nhiều!','approved',11,DATEADD(HOUR,-3,GETDATE())),
(27,N'Phạm Tiến Dũng','dung.pham@gmail.com',N'Mình thử Pinecone thấy tốt hơn ChromaDB cho production nhờ managed service. ChromaDB thì tiện cho local dev.','approved',7,DATEADD(HOUR,-5,GETDATE())),

-- Bài 28 - Stable Diffusion
(28,N'Vũ Đình Khải','khai.vu@gmail.com',N'SD 3.5 thật sự cải thiện rất nhiều về anatomy! Trước đây hay bị lỗi tay nhưng giờ đã ổn hơn nhiều.','approved',9,DATEADD(HOUR,-6,GETDATE())),
(28,N'Hoàng Lan Anh','anh.hoang@gmail.com',N'RTX 3060 12GB của mình chạy SD 3.5 khá ổn với các model đã quantize. Tốc độ generate mỗi ảnh khoảng 8 giây.','approved',5,DATEADD(HOUR,-8,GETDATE())),

-- Bài 30 - Agentic AI
(30,N'Đỗ Văn Minh','minh.do@gmail.com',N'Agentic AI đang là trend của năm 2025! Mình đang dùng CrewAI và ấn tượng với khả năng multi-agent collaboration.','approved',16,DATEADD(HOUR,-1,GETDATE())),
(30,N'Ngô Thị Thu','thu.ngo@gmail.com',N'Phần AutoGen của Microsoft cũng rất mạnh. Team mình đang dùng cho automation testing với 5 agent làm việc song song.','approved',12,DATEADD(HOUR,-2,GETDATE())),

-- Bài 31 - Rust
(31,N'Bùi Quang Sáng','sang.bui@gmail.com',N'Học Rust khó nhưng một khi hiểu borrow checker thì viết code an toàn hơn hẳn. Không bao giờ lo memory leak nữa!','approved',10,DATEADD(HOUR,-10,GETDATE())),
(31,N'Trịnh Văn Hùng','hung.trinh@gmail.com',N'Rust trong Linux kernel là bước ngoặt lịch sử. Hi vọng Windows cũng migrate hoàn toàn sớm để giảm thiểu CVE.','approved',8,DATEADD(HOUR,-12,GETDATE())),

-- Bài 33 - GraphQL vs REST
(33,N'Lý Thị Hoa','hoa.ly@gmail.com',N'Team mình vừa migrate từ REST sang GraphQL. Ban đầu phức tạp hơn nhưng giờ frontend developer tự lấy data họ cần, không cần hỏi backend nữa!','approved',13,DATEADD(HOUR,-14,GETDATE())),

-- Bài 36 - Spring Boot Virtual Threads
(36,N'Đinh Công Tú','tu.dinh@gmail.com',N'Virtual Threads là killer feature của Java 21! Chúng tôi đã test và throughput tăng gần 4 lần cho REST API có nhiều I/O. Không cần reactive nữa!','approved',18,DATEADD(HOUR,-20,GETDATE())),
(36,N'Cao Thị Mỹ Linh','linh.cao@gmail.com',N'Một dòng config mà tăng được hiệu năng nhiều vậy thật sự rất ấn tượng. Spring Boot ngày càng giỏi hơn trong việc đơn giản hóa mọi thứ.','approved',9,DATEADD(HOUR,-22,GETDATE())),

-- Bài 38 - Meta Llama 4
(38,N'Phan Văn Quý','quy.phan@gmail.com',N'Llama 4 chứng minh AI open-source đã sánh ngang closed-source. Đây là tin tuyệt vời cho developer không muốn phụ thuộc vào OpenAI!','approved',21,DATEADD(HOUR,-1,GETDATE())),
(38,N'Nguyễn Thị Thanh','thanh.nguyen@gmail.com',N'Mình đã chạy Llama 4 70B quantized trên RTX 4090 và thực sự ấn tượng! Gần bằng GPT-4 mà chạy offline hoàn toàn.','approved',17,DATEADD(HOUR,-2,GETDATE())),

-- Bài 39 - Tesla Optimus
(39,N'Hoàng Minh Khoa','khoa.hoang@gmail.com',N'Robot AI làm việc trong nhà máy thực sự đang thay đổi thế giới. Hi vọng Việt Nam cũng sẽ có nhà máy ứng dụng robot AI sớm.','approved',14,DATEADD(HOUR,-3,GETDATE())),

-- Bài 41 - Việt Nam AI
(41,N'Trần Thị Phương','phuong.tran@gmail.com',N'Rất tự hào khi Việt Nam lọt top 10 AI châu Á! Ngành IT Việt Nam đang phát triển rất mạnh, nhiều cơ hội cho các bạn trẻ theo đuổi AI.','approved',22,DATEADD(HOUR,-7,GETDATE())),
(41,N'Lê Văn Toàn','toan.le@gmail.com',N'Chứng kiến sự phát triển của AI ở Việt Nam trong 3 năm qua thực sự ấn tượng. Từ một nước outsourcing, giờ đã có nhiều product company AI đẳng cấp quốc tế.','approved',15,DATEADD(HOUR,-9,GETDATE())),

-- Bài 42 - Terraform vs Pulumi
(42,N'Vũ Thành Nam','nam.vu@gmail.com',N'Team mình dùng Terraform đã 3 năm và rất hài lòng. Provider ecosystem của Terraform thực sự không có đối thủ.','approved',7,DATEADD(HOUR,-9,GETDATE())),

-- Bài 43 - CI/CD GitHub Actions
(43,N'Đặng Thị Lan','lan.dang@gmail.com',N'GitHub Actions đã thay thế hoàn toàn Jenkins trong team mình. Đơn giản hơn, không cần maintain server và marketplace action rất phong phú.','approved',12,DATEADD(HOUR,-11,GETDATE())),

-- Bài 45 - OWASP Top 10
(45,N'Nguyễn Bá Thịnh','thinh.nguyen@gmail.com',N'LLM Injection xuất hiện trong OWASP Top 10 là tín hiệu rõ ràng rằng bảo mật AI đang là vấn đề thực sự cần giải quyết ngay.','approved',19,DATEADD(HOUR,-15,GETDATE())),
(45,N'Phạm Thị Kim Oanh','oanh.pham@gmail.com',N'Broken Access Control đứng đầu năm thứ 4 liên tiếp thật đáng lo. Nhiều developer vẫn chưa hiểu đúng về authorization và permission model.','approved',11,DATEADD(HOUR,-16,GETDATE())),

-- Bài 49 - Dùng AI tăng năng suất
(49,N'Trần Hoàng Long','long.tran@gmail.com',N'Workflow Copilot + Claude + ChatGPT của bạn chính xác là những gì mình đang làm! Tiết kiệm được khoảng 3 tiếng mỗi ngày, không ngoa chút nào.','approved',24,DATEADD(HOUR,-4,GETDATE())),
(49,N'Lê Thị Bích Ngọc','ngoc.le2@gmail.com',N'Mình cũng thấy Claude tốt nhất cho code review và architecture. GPT-4 giỏi explain code cho beginner hơn.','approved',16,DATEADD(HOUR,-5,GETDATE())),
(49,N'Nguyễn Trọng Nghĩa','nghia.nguyen@gmail.com',N'Bài viết rất thực tế! Mình sẽ thử workflow này ngay tuần tới. Trước giờ chỉ dùng Copilot thôi, chưa kết hợp Claude.','approved',8,DATEADD(HOUR,-6,GETDATE())),

-- Bài 50 - PostgreSQL vs MySQL
(50,N'Hoàng Quốc Việt','viet.hoang@gmail.com',N'Đã migrate từ MySQL sang PostgreSQL 2 năm trước và không hối hận! JSONB + pgvector cho AI app của mình giờ không thể thiếu được.','approved',20,DATEADD(HOUR,-6,GETDATE())),
(50,N'Bùi Thị Hải Yến','yen.bui@gmail.com',N'pgvector là lý do mình chọn PostgreSQL cho project RAG. Không cần thêm vector database riêng, tiết kiệm cả chi phí lẫn độ phức tạp hệ thống.','approved',15,DATEADD(HOUR,-8,GETDATE()));
GO

PRINT N'===================================';
PRINT N'Seed data MỞ RỘNG PHẦN 2 thành công!';
PRINT N'25 bài viết mới (ID 26-50)';
PRINT N'20 tags mới (ID 31-50)';
PRINT N'30 comments mới';
PRINT N'50+ ArticleTags mới';
PRINT N'===================================';
GO
------------------------------------------------------------------------------------------------
-- WISDOM IT NEWS — Seed Data Mở Rộng PHẦN 3
-- Tiếp nối Phần 1 & 2: Bổ sung bài viết (51–75), Tags (51–60) và Comments
-- Tác giả: Gemini (Hỗ trợ sinh viên FPT Polytechnic)

USE WisdomITNews;
GO

-- ===================== TAGS MỚI (51–60) =====================
SET IDENTITY_INSERT Tags ON;
INSERT INTO Tags (Id, Name, Slug, CreatedAt) VALUES
(51, N'Game Development', 'game-development', GETDATE()),
(52, N'Unity',             'unity',            GETDATE()),
(53, N'Unreal Engine',     'unreal-engine',    GETDATE()),
(54, N'Quantum Computing', 'quantum-computing', GETDATE()),
(55, N'IoT',               'iot',              GETDATE()),
(56, N'Big Data',          'big-data',         GETDATE()),
(57, N'SEO',               'seo',              GETDATE()),
(58, N'UI/UX Design',      'ui-ux-design',     GETDATE()),
(59, N'Web3',              'web3',             GETDATE()),
(60, N'Metaverse',         'metaverse',        GETDATE());
SET IDENTITY_INSERT Tags OFF;
GO

-- ===================== ARTICLES MỚI (51–75) =====================
SET IDENTITY_INSERT Articles ON;
INSERT INTO Articles (Id,Title,Slug,Summary,Content,Thumbnail,CategoryId,AuthorId,Views,Status,IsFeatured,IsBreaking,PublishedAt,AiSummary,MetaTitle,MetaDesc,CreatedAt,UpdatedAt) VALUES

-- ===== AI & MACHINE LEARNING (CategoryId = 3) =====
(51,
N'Sora và tương lai của ngành điện ảnh: Khi AI tạo ra video từ văn bản chân thực đến khó tin',
'sora-va-tuong-lai-nganh-dien-anh',
N'OpenAI Sora không chỉ là một công cụ tạo video, nó đang định nghĩa lại cách chúng ta kể chuyện và sản xuất nội dung hình ảnh.',
N'<p>Sora có khả năng tạo ra các video dài tới 1 phút với chất lượng hình ảnh tuyệt vời và tuân thủ chặt chẽ các chỉ dẫn của người dùng.</p><h2>Sức mạnh của Sora</h2><ul><li>Hiểu về vật lý thế giới thực</li><li>Duy trì tính nhất quán của nhân vật</li><li>Tạo ra các khung cảnh phức tạp với nhiều góc máy</li></ul>',
'https://images.unsplash.com/photo-1633356122544-f134324a6cee?w=800&q=80',
3,1,18500,'published',1,1,DATEADD(DAY,-1,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

(52,
N'AI trong Y tế: Cách Machine Learning giúp chẩn đoán ung thư sớm hơn 2 năm',
'ai-trong-y-te-chan-doan-ung-thu-som',
N'Các thuật toán Deep Learning mới đang giúp các bác sĩ phát hiện các dấu hiệu bệnh lý siêu nhỏ mà mắt thường không thể thấy.',
N'<p>Việc ứng dụng AI vào chẩn đoán hình ảnh đang mở ra một kỷ nguyên mới cho y học dự phòng.</p><h2>Lợi ích đột phá</h2><p>Tăng tỷ lệ sống sót nhờ phát hiện sớm và cá nhân hóa phác đồ điều trị.</p>',
'https://images.unsplash.com/photo-1576091160550-2173dba999ef?w=800&q=80',
3,1,5400,'published',0,0,DATEADD(DAY,-2,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

-- ===== LẬP TRÌNH (CategoryId = 2) =====
(53,
N'Tại sao Rust là ngôn ngữ lập trình được yêu thích nhất 9 năm liên tiếp?',
'tai-sao-rust-duoc-yeu-thich-nhat',
N'Khám phá sức hút của Rust: Sự kết hợp hoàn hảo giữa hiệu năng của C++ và sự an toàn bộ nhớ tuyệt đối.',
N'<p>Rust giải quyết được nỗi đau lớn nhất của C/C++: Memory safety mà không cần Garbage Collector.</p><h2>Những ưu điểm cốt lõi</h2><ul><li>Ownership & Borrowing system</li><li>Zero-cost abstractions</li><li>Cộng đồng hỗ trợ cực mạnh</li></ul>',
'https://images.unsplash.com/photo-1629654297299-c8506221ca97?w=800&q=80',
2,1,12300,'published',1,0,DATEADD(DAY,-3,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

(54,
N'React 19 có gì mới? Hướng dẫn sử dụng React Compiler và Actions',
'react-19-co-gi-moi-huong-dan',
N'React 19 mang đến những thay đổi mang tính cách mạng, giúp lập trình viên viết code ít hơn nhưng hiệu năng cao hơn.',
N'<p>React Compiler (React Forget) sẽ tự động tối ưu hóa việc re-render mà không cần useMemo hay useCallback.</p>',
'https://images.unsplash.com/photo-1633356122544-f134324a6cee?w=800&q=80',
2,1,8900,'published',0,0,DATEADD(DAY,-1,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

(55,
N'Lập trình hướng đối tượng (OOP) trong C: Những kỹ thuật nâng cao',
'lap-trinh-oop-trong-c-ky-thuat-nang-cao',
N'Dù C là ngôn ngữ hướng thủ tục, chúng ta vẫn có thể áp dụng các tư tưởng OOP thông qua struct và function pointer.',
N'<p>Bài viết này hướng dẫn cách giả lập tính đóng gói, kế thừa và đa hình ngay trong ngôn ngữ C thuần túy.</p>',
'https://images.unsplash.com/photo-1515879218367-8466d910aaa4?w=800&q=80',
2,1,4500,'published',0,0,DATEADD(DAY,-4,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

-- ===== GAME DEVELOPMENT (CategoryId = 2 / Dùng Tag mới) =====
(56,
N'Bắt đầu làm game với Unity: Lộ trình cho người mới từ con số 0',
'bat-dau-lam-game-voi-unity-lo-trinh',
N'Bạn muốn tạo ra trò chơi đầu tiên của mình? Unity là engine hoàn hảo để bắt đầu hành trình chinh phục thế giới game.',
N'<p>Unity sử dụng ngôn ngữ C#, có kho Asset Store khổng lồ và cộng đồng hỗ trợ rất lớn tại Việt Nam.</p>',
'https://images.unsplash.com/photo-1552824802-3a58ecf902c1?w=800&q=80',
2,1,7600,'published',1,0,DATEADD(DAY,-2,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

(57,
N'Unreal Engine 5.4: Nanite và Lumen đưa đồ họa game tiệm cận đời thực',
'unreal-engine-5-4-do-hoa-chan-thuc',
N'Khám phá những công nghệ render đỉnh cao của Epic Games giúp tạo ra những môi trường game siêu thực mà không cần nỗ lực quá lớn.',
N'<p>Với UE5, giới hạn giữa phim điện ảnh và trò chơi điện tử đang dần bị xóa nhòa.</p>',
'https://images.unsplash.com/photo-1542751371-adc38448a05e?w=800&q=80',
2,1,6200,'published',0,0,DATEADD(DAY,-5,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

-- ===== TIN CÔNG NGHỆ (CategoryId = 1) =====
(58,
N'Elon Musk ra mắt Neuralink: Con người đã có thể điều khiển máy tính bằng suy nghĩ',
'neuralink-dieu-khien-may-tinh-bang-suy-nghi',
N'Bệnh nhân đầu tiên được cấy chip Neuralink đã có thể chơi game và điều khiển chuột chỉ bằng ý nghĩ.',
N'<p>Đây là bước tiến khổng lồ trong việc hỗ trợ những người bị liệt tứ chi giao tiếp với thế giới.</p>',
'https://images.unsplash.com/photo-1507413245164-6160d8298b31?w=800&q=80',
1,1,21000,'published',1,1,DATEADD(HOUR,-12,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

(59,
N'Apple Vision Pro 2: Những rò rỉ đầu tiên về phiên bản giá rẻ hơn',
'apple-vision-pro-2-ro-ri-gia-re',
N'Apple đang nỗ lực đưa kính thực tế hỗn hợp đến gần hơn với người dùng phổ thông bằng một phiên bản rút gọn về giá.',
N'<p>Dự kiến phiên bản này sẽ loại bỏ màn hình ngoài EyeSight để giảm chi phí sản xuất.</p>',
'https://images.unsplash.com/photo-1622979135225-d2ba269cf1ac?w=800&q=80',
1,1,9800,'published',0,0,DATEADD(DAY,-1,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

-- ===== PHẦN CỨNG (CategoryId = 5) =====
(60,
N'Intel Core Ultra 9 (Series 2): Kỷ nguyên AI PC bắt đầu từ đây',
'intel-core-ultra-9-series-2-ai-pc',
N'Chip xử lý mới nhất của Intel tập trung tối đa vào hiệu năng NPU để xử lý các tác vụ AI ngay trên thiết bị.',
N'<p>Tiết kiệm điện năng hơn 40% so với thế hệ trước trong khi hiệu năng đa nhân tăng vọt.</p>',
'https://images.unsplash.com/photo-1591488320449-011701bb6704?w=800&q=80',
5,1,8500,'published',0,0,DATEADD(DAY,-3,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

(61,
N'Đánh giá Card đồ họa NVIDIA RTX 5090: Sức mạnh hủy diệt mọi tựa game 4K',
'danh-gia-nvidia-rtx-5090-suc-manh-huy-diet',
N'Siêu phẩm tiếp theo của đội xanh với kiến trúc Blackwell hứa hẹn mang lại mức hiệu năng chưa từng có.',
N'<p>Vram lên tới 32GB GDDR7, RTX 5090 không chỉ dành cho game thủ mà còn là "quái vật" cho dân làm AI.</p>',
'https://images.unsplash.com/photo-1587202372634-32705e3bf49c?w=800&q=80',
5,1,15600,'published',1,0,DATEADD(DAY,-2,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

-- ===== BẢO MẬT (CategoryId = 7) =====
(62,
N'Cảnh báo: Chiến dịch lừa đảo AI Deepfake giả danh người thân đang bùng nổ',
'canh-bao-lua-dao-deepfake-gia-danh-nguoi-than',
N'Kẻ xấu sử dụng công nghệ AI để giả dạng giọng nói và khuôn mặt người thân nhằm thực hiện các cuộc gọi lừa đảo chuyển tiền.',
N'<p>Hãy thiết lập "mật mã gia đình" để xác minh danh tính trong các cuộc gọi khẩn cấp yêu cầu tiền bạc.</p>',
'https://images.unsplash.com/photo-1550751827-4bd374c3f58b?w=800&q=80',
7,1,14200,'published',0,1,DATEADD(HOUR,-5,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

(63,
N'Zero Trust: Mô hình bảo mật tối tân cho doanh nghiệp trong kỷ nguyên Cloud',
'zero-trust-mo-hinh-bao-mat-doanh-nghiep',
N'Trong mô hình Zero Trust, không có bất kỳ thiết bị hay người dùng nào được tin tưởng mặc định, dù họ đang ở trong mạng nội bộ.',
N'<p>Xác thực liên tục, cấp quyền tối thiểu là những nguyên tắc sống còn của bảo mật hiện đại.</p>',
'https://images.unsplash.com/photo-1563013544-824ae1b704d3?w=800&q=80',
7,1,4300,'published',0,0,DATEADD(DAY,-6,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

-- ===== THỦ THUẬT IT (CategoryId = 6) =====
(64,
N'Cách tối ưu hóa Prompt Engineering để nhận được câu trả lời tốt nhất từ AI',
'cach-toi-uu-prompt-engineering-ai',
N'Việc đặt câu hỏi đúng cách quyết định 80% chất lượng kết quả mà bạn nhận được từ ChatGPT hay Claude.',
N'<p>Hãy áp dụng công thức: Vai trò + Bối cảnh + Nhiệm vụ + Định dạng đầu ra.</p>',
'https://images.unsplash.com/photo-1677442136019-21780ecad995?w=800&q=80',
6,1,11000,'published',1,0,DATEADD(DAY,-1,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

(65,
N'Tự xây dựng Home Lab để thực hành DevOps chỉ với một chiếc PC cũ',
'tu-xay-dung-home-lab-devops-pc-cu',
N'Bạn không cần server đắt tiền để học Docker, K8s hay CI/CD. Một chiếc PC cũ với Linux là đủ để bắt đầu.',
N'<p>Hướng dẫn cài đặt Proxmox hoặc dùng Docker Compose để giả lập toàn bộ hệ thống doanh nghiệp tại nhà.</p>',
'https://images.unsplash.com/photo-1558494949-ef010cbdcc51?w=800&q=80',
6,1,5800,'published',0,0,DATEADD(DAY,-2,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

-- ===== ĐIỆN TOÁN ĐÁM MÂY (CategoryId = 8) =====
(66,
N'Serverless Computing: Tập trung vào code, không lo về hạ tầng',
'serverless-computing-tap-trung-vao-code',
N'Tìm hiểu cách AWS Lambda hay Google Cloud Functions giúp bạn vận hành ứng dụng mà không cần quản lý máy chủ.',
N'<p>Chi phí chỉ tính trên số lượng request thực tế, giúp tiết kiệm tối đa ngân sách cho các dự án startup.</p>',
'https://images.unsplash.com/photo-1451187580459-43490279c0fa?w=800&q=80',
8,1,3900,'published',0,0,DATEADD(DAY,-7,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

-- ===== PHẦN MỀM (CategoryId = 4) =====
(67,
N'Microsoft Windows 12: Những tính năng AI tích hợp sâu đầu tiên được tiết lộ',
'windows-12-tinh-nang-ai-tich-hop-sau',
N'Windows 12 sẽ không chỉ là một hệ điều hành, nó sẽ là một trợ lý AI toàn năng thấu hiểu mọi thói quen của bạn.',
N'<p>Giao diện người dùng sẽ thay đổi linh hoạt dựa trên ngữ cảnh công việc mà bạn đang thực hiện.</p>',
'https://images.unsplash.com/photo-1587202372634-32705e3bf49c?w=800&q=80',
4,1,12800,'published',1,0,DATEADD(DAY,-4,GETDATE()),NULL,NULL,NULL,GETDATE(),GETDATE()),

-- ===== CÁC CHỦ ĐỀ KHÁC (Bổ sung cho đủ 25 bài) =====
(68, N'Tương lai của Web3: Liệu có thay thế được Internet hiện tại?', 'tuong-lai-web3-internet', N'Web3 hứa hẹn quyền sở hữu dữ liệu thực sự cho người dùng.', N'<p>Blockchain không chỉ dành cho tiền số, nó là nền tảng cho sự minh bạch.</p>', 'https://images.unsplash.com/photo-1639762681485-074b7f938ba0?w=800&q=80', 1,1,4200,'published',0,0,GETDATE(),NULL,NULL,NULL,GETDATE(),GETDATE()),
(69, N'UI/UX 2025: Xu hướng thiết kế tối giản hay phức tạp lên ngôi?', 'ui-ux-2025-xu-huong-thiet-ke', N'Sự lên ngôi của giao diện điều khiển bằng giọng nói và cử chỉ.', N'<p>Thiết kế không còn chỉ nằm trên màn hình phẳng.</p>', 'https://images.unsplash.com/photo-1586717791821-3f44a563eb4c?w=800&q=80', 4,1,5100,'published',0,0,GETDATE(),NULL,NULL,NULL,GETDATE(),GETDATE()),
(70, N'Học Big Data bắt đầu từ đâu? Các kỹ năng cần có năm 2025', 'hoc-big-data-bat-dau-tu-dau', N'Dữ liệu là dầu mỏ mới, và người biết khai thác nó sẽ nắm giữ tương lai.', N'<p>Học SQL, Python và các công cụ như Spark, Hadoop.</p>', 'https://images.unsplash.com/photo-1551288049-bbda38a10472?w=800&q=80', 2,1,6700,'published',0,0,GETDATE(),NULL,NULL,NULL,GETDATE(),GETDATE()),
(71, N'Internet of Things (IoT) và những ứng dụng trong Smart City', 'iot-va-smart-city', N'Khi mọi vật dụng quanh ta đều kết nối và trò chuyện với nhau.', N'<p>Giao thông thông minh, quản lý năng lượng tự động.</p>', 'https://images.unsplash.com/photo-1518770660439-4636190af475?w=800&q=80', 1,1,3800,'published',0,0,GETDATE(),NULL,NULL,NULL,GETDATE(),GETDATE()),
(72, N'Quantum Computing: Khi máy tính lượng tử giải mã những bí mật của vũ trụ', 'quantum-computing-bi-mat-vu-tru', N'Sức mạnh tính toán gấp hàng tỷ lần máy tính truyền thống.', N'<p>Thách thức mọi hệ thống mã hóa hiện nay.</p>', 'https://images.unsplash.com/photo-1635070041078-e363dbe005cb?w=800&q=80', 3,1,7200,'published',0,0,GETDATE(),NULL,NULL,NULL,GETDATE(),GETDATE()),
(73, N'SEO trong kỷ nguyên AI Search: Làm sao để lên top Google?', 'seo-ky-nguyen-ai-search', N'Khi Google Search Generative Experience (SGE) thay đổi luật chơi.', N'<p>Content chất lượng và uy tín (E-E-A-T) trở nên quan trọng hơn bao giờ hết.</p>', 'https://images.unsplash.com/photo-1562577353-f5d40024af0b?w=800&q=80', 6,1,5900,'published',0,0,GETDATE(),NULL,NULL,NULL,GETDATE(),GETDATE()),
(74, N'Tại sao sinh viên IT nên học Docker và Kubernetes từ năm nhất?', 'tai-sao-hoc-docker-k8s-som', N'Container hóa là kỹ năng bắt buộc phải có của mọi lập trình viên hiện đại.', N'<p>Giúp project của bạn chạy mượt mà ở mọi môi trường máy tính.</p>', 'https://images.unsplash.com/photo-1605745341112-85968b193ef5?w=800&q=80', 2,1,8100,'published',0,0,GETDATE(),NULL,NULL,NULL,GETDATE(),GETDATE()),
(75, N'Xây dựng Portfolio cá nhân ấn tượng để "lọt mắt xanh" nhà tuyển dụng', 'xay-dung-portfolio-an-tuong', N'Portfolio không chỉ là nơi chứa link Github, nó là câu chuyện về hành trình của bạn.', N'<p>Hãy tập trung vào việc giải thích cách bạn giải quyết vấn đề.</p>', 'https://images.unsplash.com/photo-1507238691740-187a5b1d37b8?w=800&q=80', 6,1,9200,'published',0,0,GETDATE(),NULL,NULL,NULL,GETDATE(),GETDATE());

SET IDENTITY_INSERT Articles OFF;
GO

-- ===================== ARTICLE TAGS (Map các bài mới với tags) =====================
INSERT INTO ArticleTags (ArticleId, TagId) VALUES
(51,11),(51,42), -- Sora - AI, OpenAI
(53,33),         -- Rust
(54,7),(54,2),   -- React, JS
(55,21),         -- C# (Hoặc C tùy tag bạn có)
(56,51),(56,52), -- Game Dev, Unity
(57,51),(57,53), -- Game Dev, Unreal
(58,11),         -- AI
(60,5), (60,11), -- Phần cứng, AI
(61,5),          -- Phần cứng
(62,11),(62,10), -- AI, Bảo mật
(64,39),(64,11), -- Prompt Engineering, AI
(65,44),(65,6),  -- DevOps, Docker
(68,29),(68,59), -- Blockchain, Web3
(69,58),         -- UI/UX
(70,56),         -- Big Data
(71,55),         -- IoT
(72,54);         -- Quantum
GO

-- ===================== COMMENTS MỚI =====================
INSERT INTO Comments (ArticleId,AuthorName,AuthorEmail,Content,Status,Likes,CreatedAt) VALUES
(51,N'Hoàng Long','longh@fpt.edu.vn',N'Xem video Sora tạo ra mà nổi da gà, thật quá!','approved',25,GETDATE()),
(53,N'Minh Trí','tri.m@gmail.com',N'Rust học hơi dốc nhưng khi hiểu về Ownership rồi thì viết code rất sướng.','approved',15,GETDATE()),
(56,N'Thanh Hằng','hang.fpt@gmail.com',N'Em là sinh viên Poly đang định hướng theo Game, bài viết này rất đúng lúc.','approved',12,GETDATE()),
(58,N'Quốc Bảo','baoq@outlook.com',N'Neuralink thực sự là một phép màu cho y học.','approved',40,GETDATE()),
(62,N'Cô Lan','lan.nguyen@yahoo.com',N'Cảm ơn bài viết, dạo này nhiều cuộc gọi lạ quá, phải cảnh giác thôi.','approved',50,GETDATE()),
(64,N'Lê Anh','anhle@dev.to',N'Prompting đúng là một nghệ thuật, mình thử theo công thức này kết quả khác hẳn.','approved',18,GETDATE());
GO

PRINT N'Seed data PHẦN 3 đã được nạp thành công!';
PRINT N'Đã thêm 25 bài viết mới (ID: 51-75) với ảnh Unsplash chất lượng cao.';
-------------------------------------------------------------------------------------------------------
