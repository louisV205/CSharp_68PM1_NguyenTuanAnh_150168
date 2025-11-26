$(function () {
    const $form = $('#loginForm');
    const $submitBtn = $('#loginSubmit');
    const $spinner = $submitBtn.find('.btn-spinner');

    // Toggle password visibility
    $('.password-toggle').on('click', function () {
        const $btn = $(this);
        const $input = $btn.closest('.password-wrapper').find('input');
        const isPassword = $input.attr('type') === 'password';
        $input.attr('type', isPassword ? 'text' : 'password');
        $btn.toggleClass('visible');
    });

    // Submit form via AJAX
    $form.on('submit', function (e) {
        e.preventDefault();

        // Clear previous errors
        $('#emailError, #passwordError').text('');

        const formData = $form.serialize();
        $submitBtn.prop('disabled', true);
        $spinner.removeClass('hidden');

        $.ajax({
            url: $form.attr('action'),
            method: 'POST',
            data: formData,
            success: function (res) {
                if (res.success) {
                    // Redirect to homepage or specified URL
                    window.location.href = res.redirectUrl || '/';
                } else if (res.errors) {
                    // Show field-specific errors
                    if (res.errors.Email) {
                        $('#emailError').text(res.errors.Email.join(', '));
                    }
                    if (res.errors.Password) {
                        $('#passwordError').text(res.errors.Password.join(', '));
                    }
                    // Show global errors
                    if (res.errors._global) {
                        alert(res.errors._global.join('\n'));
                    }
                } else {
                    alert('Đăng nhập không thành công. Vui lòng thử lại.');
                }
            },
            error: function () {
                alert('Có lỗi xảy ra khi gửi yêu cầu. Vui lòng thử lại sau.');
            },
            complete: function () {
                $submitBtn.prop('disabled', false);
                $spinner.addClass('hidden');
            }
        });
    });
});